using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SamFirmDownloader.Helper;

internal static class WebHelper
{
    private static readonly IHttpClientFactory httpClientFactory = App.ServiceProvider.GetRequiredService<IHttpClientFactory>();

    private static int interval = 0;
    private static long lastBread = 0;
    private static int lastSpeed = 0;

    private static string? JSessionID { get; set; }

    private static string? EncryptedNonce { get; set; }

    private static byte[]? DecryptedNonce { get; set; }

    private static string? Auth { get; set; }

    private static AuthenticationHeaderValue AuthHeaderWithNonce =>
        new("FUS", $"nonce=\"{EncryptedNonce}\", signature=\"{Auth}\", nc=\"\", type=\"\", realm=\"\", newauth=\"1\"");

    private static AuthenticationHeaderValue AuthHeaderNoNonce =>
        new("FUS", $"nonce=\"\", signature=\"{Auth}\", nc=\"\", type=\"\", realm=\"\", newauth=\"1\"");

    public static string? Nonce
    {
        get
        {
            if (DecryptedNonce == null)
                return null;
            return Encoding.UTF8.GetString(DecryptedNonce);
        }
    }

    public static Action<string>? SpeedAction { get; set; }

    public static Action<double>? ProgressAction { get; set; }

    private static void GetResponseFUS(HttpResponseMessage response)
    {
        try
        {
            if (response.Headers.Contains("Set-Cookie"))
            {
                JSessionID = response.Headers
                    .GetValues("Set-Cookie")
                    .FirstOrDefault()?
                    .Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(x => x.Contains("JSESSIONID"))?
                    .Split(['='], StringSplitOptions.RemoveEmptyEntries)[1];
            }
            if (response.Headers.Contains("NONCE"))
            {
                EncryptedNonce = response.Headers.GetValues("NONCE").FirstOrDefault();
                if (EncryptedNonce != null)
                {
                    DecryptedNonce = KiesHelper.DecryptNonce(EncryptedNonce);
                    Auth = KiesHelper.GetAuth(DecryptedNonce);
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog("Error getting response: " + ex.Message);
        }
    }

    private static HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage { RequestUri = new Uri(url) };
        request.Headers.Add("Cache-Control", "no-cache");
        request.Headers.Add("User-Agent", "Kies2.0_FUS");
        request.Headers.Authorization = new("FUS", "nonce=\"\", signature=\"\", nc=\"\", type=\"\", realm=\"\"");
        request.Headers.Add("Cookie", $"JSESSIONID={JSessionID}");
        return request;
    }

    private static async Task<(int, string?)> XMLFUSRequest(string URL, string xml)
    {
        var request = CreateRequest(URL);
        request.Method = HttpMethod.Post;
        request.Headers.Authorization = AuthHeaderNoNonce;

        var sanitizedXml = xml.ReplaceLineEndings()
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\t", string.Empty)
            .Trim();
        request.Content = new StringContent(sanitizedXml, Encoding.ASCII, "application/xml");

        using var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);
        if (response == null)
            return (901, null);

        GetResponseFUS(response);

        string? xmlresponse = null;
        if (response.IsSuccessStatusCode)
        {
            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream);
            xmlresponse = await reader.ReadToEndAsync();
        }
        return ((int)response.StatusCode, xmlresponse);
    }

    private static void ResetDownloadSpeed(long _lastBread)
    {
        interval = lastSpeed = 0;
        lastBread = _lastBread;
    }

    private static int GetDownloadSpeed(long bread, Stopwatch sw)
    {
        if (!sw.IsRunning)
            sw.Start();

        if (interval < 150)
        {
            ++interval;
            return -1;
        }

        interval = 0;
        var num1 = sw.ElapsedMilliseconds / 1000d;
        var num2 = (int)Math.Floor((bread - lastBread) / num1 / 1024d);

        if (lastSpeed != 0)
            num2 = (lastSpeed + num2) / 2;

        lastSpeed = num2;
        lastBread = bread;
        sw.Reset();

        return num2;
    }

    public static double GetDownloadProgress(long value, long total)
    {
        var tmp = value * 100d / total;
        return tmp;
    }

    public static async Task<string> GetHtml(string url)
    {
        var num = 0;
        while (true)
        {
            try
            {
                using var httpClient = httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception)
            {
                if (num < 2) ++num;
                else break;
            }
        }
        return string.Empty;
    }

    public static int GenerateNonce()
    {
        var request = CreateRequest("https://neofussvr.sslcs.cdngc.net/NF_DownloadGenerateNonce.do");
        request.Method = HttpMethod.Post;
        request.Headers.Authorization = new("FUS", $"nonce=\"\", signature=\"\", nc=\"\", type=\"\", realm=\"\", newauth=\"1\"");
        request.Content = new StringContent(string.Empty);

        using var client = httpClientFactory.CreateClient();
        using var response = client.Send(request);
        if (response == null)
            return 901;

        GetResponseFUS(response);

        var statusCode = (int)response.StatusCode;
        return statusCode;
    }

    public static async Task<(int, string?)> DownloadBinaryInform(string xml)
    {
        return await XMLFUSRequest("https://neofussvr.sslcs.cdngc.net/NF_DownloadBinaryInform.do", xml);
    }

    public static async Task<(int, string?)> DownloadBinaryInit(string xml)
    {
        return await XMLFUSRequest("https://neofussvr.sslcs.cdngc.net/NF_DownloadBinaryInitForMass.do", xml);
    }

    public static async Task<int> DownloadBinary(string path, string file, string saveTo, string size, CancellationToken cancellationToken)
    {
        long bytesTransferred = 0;

        var request = CreateRequest("http://cloud-neofussvr.samsungmobile.com/NF_DownloadBinaryForMass.do?file=" + path + file);
        request.Method = HttpMethod.Get;
        request.Headers.Authorization = AuthHeaderWithNonce;

        var saveFile = new FileInfo(saveTo);
        if (saveFile.Exists)
        {
            var existingFileSize = saveFile.Length;
            if (long.Parse(size) == existingFileSize)
            {
                LogHelper.WriteLog("File already downloaded.");
                return 200;
            }

            LogHelper.WriteLog("File exists. Resuming download...");
            request.Headers.Range = new RangeHeaderValue(existingFileSize, null);
            bytesTransferred = existingFileSize;
        }

        using var httpClient = httpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
        {
            LogHelper.WriteLog($"Error downloading: {(int)response.StatusCode}");
            return (int)response.StatusCode;
        }

        GetResponseFUS(response);

        var totalSize = bytesTransferred + (response.Content.Headers.ContentLength ?? 0);
        if (!saveFile.Exists || saveFile.Length != totalSize)
        {
            ResetDownloadSpeed(bytesTransferred);

            var buffer = new byte[256 * 1024];
            var sw = new Stopwatch();
            try
            {
                using var contentStream = await response.Content.ReadAsStreamAsync(CancellationToken.None);
                using var fileStream = new FileStream(saveTo, FileMode.Append, FileAccess.Write, FileShare.None, buffer.Length, true);
                while (true)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, CancellationToken.None);
                    if (bytesRead <= 0)
                        break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), CancellationToken.None);
                    bytesTransferred += bytesRead;

                    var dlspeed = GetDownloadSpeed(bytesTransferred, sw);
                    if (dlspeed != -1)
                    {
                        var speed = dlspeed;
                        var sizeInMB = (int)(bytesTransferred / 1024 / 1024);
                        var totalSizeInMB = (int)(totalSize / 1024 / 1024);
                        var progress = GetDownloadProgress(bytesTransferred, totalSize);
                        SpeedAction?.Invoke($"Speed: {speed} KB/s, {sizeInMB} MB / {totalSizeInMB} MB");
                        ProgressAction?.Invoke(progress);
                    }

                    if (cancellationToken.IsCancellationRequested)
                        return -2;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"Download has error: {ex}");
                return -1;
            }
        }
        return (int)response.StatusCode;
    }
}
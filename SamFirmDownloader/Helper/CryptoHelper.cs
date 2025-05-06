using ICSharpCode.SharpZipLib.Zip;
using SamFirmDownloader.Models;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace SamFirmDownloader.Helper;

internal class CryptoHelper
{
    private static byte[]? KEY;

    public static Action<double>? ZipProcess { get; set; }

    public static Action<double>? TransferredProcess { get; set; }

    private static string GetLogicCheck(string input, string nonce)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var num1 = 0;
        if (input.EndsWith(".zip.enc2") || input.EndsWith(".zip.enc4"))
            num1 = input.Length - 25;

        var stringBuilder = new StringBuilder();
        foreach (int num2 in nonce)
        {
            int num3 = num2 & 15;
            if (input.Length <= num3 + num1)
                return string.Empty;

            stringBuilder.Append(input[num3 + num1]);
        }

        return stringBuilder.ToString();
    }

    private static double GetProgress(long value, long total)
    {
        var tmp = value * 100d / total;
        return tmp;
    }

    public static void SetDecryptKey(string region, string model, string version)
    {
        var sb = new StringBuilder();
        sb.Append(region);
        sb.Append(':');
        sb.Append(model);
        sb.Append(':');
        sb.Append(version);

        var bytes = Encoding.ASCII.GetBytes(sb.ToString());
        KEY = MD5.HashData(bytes);
    }

    public static void SetDecryptKey(string version, string LogicValue)
    {
        var bytes = Encoding.ASCII.GetBytes(GetLogicCheck(version, LogicValue));
        KEY = MD5.HashData(bytes);
    }

    public static async Task<int> DecryptAndUnzip(string encryptedFile, string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (KEY == null)
        {
            LogHelper.WriteLog("Error decrypting file: Key is not set.");
            return 1;
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.BlockSize = 128;
        aes.Padding = PaddingMode.PKCS7;

        try
        {
            LogHelper.WriteLog($"Please note that the sum of unzipped files might be larger than the downloaded firmware file");

            using var decryptor = aes.CreateDecryptor(KEY, null);
            using var encryptedFileStream = new FileStream(encryptedFile, FileMode.Open);
            using var cryptoStream = new CryptoStream(encryptedFileStream, decryptor, CryptoStreamMode.Read);
            using var zipInput = new ZipInputStream(cryptoStream, 256 * 1024);

            var data = new byte[256 * 1024];
            var bytesRead = 0L;
            var fileSize = encryptedFileStream.Length;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var entry = zipInput.GetNextEntry();
                if (entry == null)
                    break;

                if (!entry.IsFile)
                    continue;

                if (entry.CanDecompress)
                {
                    fileSize -= entry.CompressedSize;
                    fileSize += entry.Size;

                    var outputFile = Path.Combine(outputDirectory, entry.Name);
                    var directory = Path.GetDirectoryName(outputFile);
                    if (directory == null)
                        continue;

                    if (!Directory.Exists(directory))
                    {
                        LogHelper.WriteLog($"Creating directory {directory}");
                        Directory.CreateDirectory(directory);
                    }

                    LogHelper.WriteLog($"Writing file {outputFile}");

                    using var fileStream2 = new FileStream(outputFile, FileMode.Create);
                    while (true)
                    {
                        var size = await zipInput.ReadAsync(data, CancellationToken.None);
                        if (size <= 0)
                            break;

                        bytesRead += size;
                        await fileStream2.WriteAsync(data.AsMemory(0, size), CancellationToken.None);

                        if (cancellationToken.IsCancellationRequested)
                            break;

                        ZipProcess?.Invoke(GetProgress(bytesRead, Math.Max(bytesRead, fileSize)));
                        TransferredProcess?.Invoke(bytesRead);
                    }

                    try { File.SetLastWriteTime(outputFile, entry.DateTime); }
                    catch { }
                }
                else
                {
                    bytesRead += entry.Size;
                    ZipProcess?.Invoke(GetProgress(bytesRead, Math.Max(bytesRead, fileSize)));
                    TransferredProcess?.Invoke(bytesRead);
                }
            }
        }
        catch (CryptographicException)
        {
            LogHelper.WriteLog("Error decrypting file: Wrong key.");
            return 2;
        }
        catch (TargetInvocationException)
        {
            LogHelper.WriteLog("Error decrypting file: Please turn off FIPS compliance checking.");
            return 3;
        }
        catch (IOException ex)
        {
            LogHelper.WriteLog("Error decrypting file: IOException: " + ex.Message);
            return 4;
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog("Error decrypting file: Exception: " + ex.Message);
            return 5;
        }
        return 0;
    }

    public static async Task SaveMeta(Firmware fw, string metafile)
    {
        if (fw.Version == null)
            return;

        try
        {
            var directory = Path.GetDirectoryName(metafile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(Path.GetDirectoryName(metafile)))
                Directory.CreateDirectory(directory);

            using var text = File.CreateText(metafile);
            text.WriteLine("[SamFirmData]");
            text.WriteLine("Model=" + fw.Model);
            text.WriteLine("Devicename=" + fw.DisplayName);
            text.WriteLine("Region=" + fw.Region);
            text.WriteLine("Version=" + fw.Version);
            text.WriteLine("OS=" + fw.OS);
            text.WriteLine("Filesize=" + fw.Size);
            text.WriteLine("Filename=" + fw.Filename);
            text.WriteLine("ReleaseDate=" + fw.LastModified);
            await text.FlushAsync();
            text.Close();
        }
        catch { }
    }
}
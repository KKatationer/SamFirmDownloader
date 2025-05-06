using SamFirmDownloader.Models;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SamFirmDownloader.Helper;

internal static partial class FirmwareHelper
{
    [GeneratedRegex("^(.*)/(.*)/$")]
    private static partial Regex SamMobileRegex();

    [GeneratedRegex("PDA:</b> ([^\\s]+) <b>")]
    private static partial Regex PDARegex();

    [GeneratedRegex("CSC:</b> ([^\\s]+) <b>")]
    private static partial Regex CSCRegex();

    private static readonly IReadOnlyList<Func<string, string, Task<string>>> FWFetchFuncs =
    [
        new Func<string, string, Task<string>>(OTAInfoFetch1),
        new Func<string, string, Task<string>>(OTAInfoFetch2),
        new Func<string, string, Task<string>>(SamsungFirmwareOrgFetch1),
        new Func<string, string, Task<string>>(SamsungFirmwareOrgFetch2),
        new Func<string, string, Task<string>>(SamMobileFetch1),
        new Func<string, string, Task<string>>(SamMobileFetch2),
        new Func<string, string, Task<string>>(SamsungUpdatesFetch)
    ];

    private static string? SamsungFirmwareOrgHtml;
    private static string? SamMobileHtml;

    private static async Task<string?> OTAInfoFetch(string model, string region, bool latest = true)
    {
        try
        {
            var xml = await WebHelper.GetHtml("http://fota-cloud-dn.ospserver.net/firmware/" + region + "/" + model + "/version.xml");
            return !latest ?
                XmlHelper.GetXMLValue(xml, "firmware/version/upgrade/value", null, null)?.ToUpper() :
                XmlHelper.GetXMLValue(xml, "firmware/version/latest", null, null)?.ToUpper();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> OTAInfoFetch1(string model, string region)
    {
        return await OTAInfoFetch(model, region, true) ?? string.Empty;
    }

    private static async Task<string> OTAInfoFetch2(string model, string region)
    {
        return await OTAInfoFetch(model, region, false) ?? string.Empty;
    }

    private static string GetInfoSFO(string html, string search)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var num = html.IndexOf(">" + search + "<");
        if (num < 0)
            return string.Empty;

        var startIndex = num + search.Length + 1 + 19;
        var str = html[startIndex..];
        return str[..str.IndexOf('<')];
    }

    private static async Task<string> SamsungFirmwareOrgFetch(string model, string _)
    {
        var samsungFirmwareOrgHtml = SamsungFirmwareOrgHtml;
        if (string.IsNullOrEmpty(samsungFirmwareOrgHtml))
            return string.Empty;

        var startIndex = samsungFirmwareOrgHtml.IndexOf("\"/model/" + model + "/\"");
        if (startIndex < 0)
            return string.Empty;

        var s = samsungFirmwareOrgHtml[startIndex..];
        var url = "https://samsung-firmware.org";
        using var stringReader = new StringReader(s);
        while (true)
        {
            var str = stringReader.ReadLine();
            if (string.IsNullOrEmpty(str))
                break;

            if (str.Contains("Download"))
            {
                var num = str.IndexOf('"');
                var length = str[(num + 1)..].IndexOf('"');
                url += str.Substring(num + 1, length);
                break;
            }
        }

        var html = await WebHelper.GetHtml(url);
        var infoSfo1 = GetInfoSFO(html, "PDA Version");
        var infoSfo2 = GetInfoSFO(html, "CSC Version");
        var infoSfo3 = GetInfoSFO(html, "PHONE Version");
        if (string.IsNullOrEmpty(infoSfo1) || string.IsNullOrEmpty(infoSfo2) || string.IsNullOrEmpty(infoSfo3))
            return string.Empty;

        return $"{infoSfo1}/{infoSfo2}/{infoSfo3}";
    }

    private static async Task<string> SamsungFirmwareOrgFetch1(string model, string region)
    {
        SamsungFirmwareOrgHtml = await WebHelper.GetHtml("https://samsung-firmware.org/model/" + model + "/region/" + region + "/");
        return await SamsungFirmwareOrgFetch(model, region);
    }

    private static async Task<string> SamsungFirmwareOrgFetch2(string model, string region)
    {
        var str = await SamsungFirmwareOrgFetch(model, region);
        if (!string.IsNullOrEmpty(str))
        {
            string[] strArray = str.Split('/');
            str = strArray[0] + "/" + strArray[2] + "/" + strArray[1];
        }
        return str;
    }

    private static string TdExtract(string line)
    {
        int startIndex = line.IndexOf("<td>") + 4;
        int num = line.IndexOf("</td>");
        if (startIndex < 0 || num < 0)
            return string.Empty;
        return line[startIndex..num];
    }

    private static async Task<string> SamMobileFetch(string model, string region, int index)
    {
        var samMobileHtml = SamMobileHtml;
        if (string.IsNullOrEmpty(samMobileHtml) || samMobileHtml.Contains("Device model not found"))
            return string.Empty;

        var num = 0;
        while (index-- >= 0 && num >= 0)
            num = samMobileHtml.IndexOf("<a class=\"firmware-table-row\" href=\"", num + 1);
        if (num < 0)
            return string.Empty;

        var str1 = samMobileHtml[(num + 36)..];
        var html = await WebHelper.GetHtml(str1[..str1.IndexOf('"')]);
        var input = string.Empty;
        var flag = false;
        using var stringReader = new StringReader(html);
        while (true)
        {
            var line = stringReader.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;

            var str2 = TdExtract(line).Trim();
            if (str2 == "PDA" || str2 == "CSC")
                flag = true;
            else if (flag)
            {
                input = input + str2 + "/";
                flag = false;
            }
        }

        return SamMobileRegex().Replace(input, "$1/$2/$1");
    }

    private static async Task<string> SamMobileFetch1(string model, string region)
    {
        SamMobileHtml = await WebHelper.GetHtml("http://www.sammobile.com/firmwares/database/" + model + "/" + region + "/");
        return await SamMobileFetch(model, region, 0);
    }

    private static async Task<string> SamMobileFetch2(string model, string region)
    {
        return await SamMobileFetch(model, region, 1);
    }

    private static async Task<string> SamsungUpdatesFetch(string model, string region)
    {
        try
        {
            var s = await WebHelper.GetHtml("http://samsung-updates.com/device/?id=" + model);
            var address = "http://samsung-updates.com";
            var flag = false;
            using var stringReader = new StringReader(s);
            while (true)
            {
                var str = stringReader.ReadLine();
                if (string.IsNullOrEmpty(str))
                    continue;

                if (str.Contains("/" + model + "/" + region + "/"))
                {
                    int num = str.IndexOf("a href=\"");
                    int length = str[(num + 8)..].IndexOf('"');
                    address += str.Substring(num + 8, length);
                    flag = true;
                    break;
                }
            }

            if (!flag)
                return string.Empty;

            var input = await WebHelper.GetHtml(address);
            var match1 = PDARegex().Match(input);
            if (!match1.Success)
                return string.Empty;

            var format = match1.Groups[1].Value + "/{0}/" + match1.Groups[1].Value;
            var match2 = CSCRegex().Match(input);
            if (!match2.Success)
                return string.Empty;

            return string.Format(format, match2.Groups[1].Value);
        }
        catch (WebException)
        {
            return string.Empty;
        }
    }

    private static string ExtractInfo(string info, string type)
    {
        var strArray = info.Split('/');
        if (strArray.Length < 2)
            return string.Empty;
        switch (type)
        {
            case "pda":
                return strArray[0];

            case "csc":
                return strArray[1];

            case "phone":
                if (strArray.Length < 3 || string.IsNullOrEmpty(strArray[2]))
                    return strArray[0];
                return strArray[2];

            case "data":
                if (strArray.Length < 4)
                    return strArray[0];
                return strArray[3];

            default:
                return string.Empty;
        }
    }

    private static int GetXMLStatusCode(string? xml)
    {
        if (string.IsNullOrEmpty(xml))
            return 0;
        if (int.TryParse(XmlHelper.GetXMLValue(xml, "FUSBody/Results/Status", null, null), out int result))
            return result;
        return 666;
    }

    public static async Task<Firmware> UpdateCheck(string model, string region, string imei, string pda, string csc, string phone, string data, bool BinaryNature, bool AutoFetch = false)
    {
        LogHelper.WriteLog($"Checking firmware for {model}/{region}/{pda}/{csc}/{phone}/{data}");

        var firmware = new Firmware();
        var noncestatus = WebHelper.GenerateNonce();
        if (noncestatus != 200)
        {
            LogHelper.WriteLog($"Error: Could not generate Nonce. Status code: {noncestatus}");
            firmware.ConnectionError = true;
            return firmware;
        }

        var xmlbinaryInform = XmlHelper.GetXmlBinaryInform(model, region, imei, pda, csc, phone, data, BinaryNature);
        var xmlbinaryData = await WebHelper.DownloadBinaryInform(xmlbinaryInform);
        var htmlstatus = xmlbinaryData.Item1;
        var xmlresponse = xmlbinaryData.Item2;
        var xmlstatus = GetXMLStatusCode(xmlresponse);
        if (htmlstatus != 200 || xmlstatus != 200)
        {
            LogHelper.WriteLog($"Error: Could not send BinaryInform. Status code: {htmlstatus} / {xmlstatus}");
            return firmware;
        }

        if (string.IsNullOrEmpty(xmlresponse))
            return firmware;

        firmware.Version = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Results/LATEST_FW_VERSION/Data") ?? string.Empty;
        firmware.Model = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/DEVICE_MODEL_NAME/Data") ?? string.Empty;
        firmware.DisplayName = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/DEVICE_MODEL_DISPLAYNAME/Data") ?? string.Empty;
        firmware.OS = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/LATEST_OS_VERSION/Data") ?? string.Empty;
        firmware.LastModified = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/LAST_MODIFIED/Data") ?? string.Empty;
        firmware.Filename = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/BINARY_NAME/Data") ?? string.Empty;
        firmware.Size = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/BINARY_BYTE_SIZE/Data") ?? string.Empty;
        var xmlValue = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/BINARY_CRC/Data") ?? string.Empty;
        if (!string.IsNullOrEmpty(xmlValue))
            firmware.CRC = [.. BitConverter.GetBytes(Convert.ToUInt32(xmlValue)).Reverse()];
        firmware.Model_Type = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/DEVICE_MODEL_TYPE/Data") ?? string.Empty;
        firmware.Path = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/MODEL_PATH/Data") ?? string.Empty;
        firmware.Region = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/DEVICE_LOCAL_CODE/Data") ?? string.Empty;
        xmlValue = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/BINARY_NATURE/Data");
        if (!string.IsNullOrEmpty(xmlValue))
            firmware.BinaryNature = int.Parse(xmlValue);
        if (XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/LOGIC_OPTION_FACTORY/Data") == "1")
            firmware.LogicValueFactory = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/LOGIC_VALUE_FACTORY/Data") ?? string.Empty;
        if (XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/LOGIC_OPTION_HOME/Data") == "1")
            firmware.LogicValueHome = XmlHelper.GetXMLValue(xmlresponse, "FUSBody/Put/LOGIC_VALUE_HOME/Data") ?? string.Empty;
        if (!AutoFetch)
        {
            if (pda + "/" + csc + "/" + phone + "/" + pda == firmware.Version)
                LogHelper.WriteLog("Current firmware is latest!");
            else
                LogHelper.WriteLog("Newer firmware available!");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Model: " + firmware.Model);
        sb.AppendLine("Version: " + firmware.Version);
        sb.AppendLine("OS: " + firmware.OS);
        sb.AppendLine("Filename: " + firmware.Filename);
        sb.AppendLine("Size: " + firmware.Size + " bytes");
        if (firmware.BinaryNature == 1)
            sb.Append("LogicValue: " + firmware.LogicValueFactory);
        else
            sb.Append("LogicValue: " + firmware.LogicValueHome);
        LogHelper.WriteLog(sb.ToString());

        return firmware;
    }

    public static async Task<Firmware> UpdateCheck(string model, string region, string imei, bool binaryNature)
    {
        var firmware = new Firmware();

        foreach (var fwFetchFunc in FWFetchFuncs)
        {
            var info = await fwFetchFunc(model, region);
            if (string.IsNullOrEmpty(info))
                continue;

            var pda = ExtractInfo(info, "pda");
            if (string.IsNullOrEmpty(pda))
                return new Firmware();

            var csc = ExtractInfo(info, "csc");
            var phone = ExtractInfo(info, "phone");
            var data = ExtractInfo(info, "data");

            firmware = await UpdateCheck(model, region, imei, pda, csc, phone, data, binaryNature, true);
            if (string.IsNullOrEmpty(firmware.Version))
            {
                if (firmware.ConnectionError)
                    break;
            }
            else
                break;
        }

        if (string.IsNullOrEmpty(firmware.Version))
            LogHelper.WriteLog($"Could not fetch info for {model}/{region}. Please verify the input or use manual info");

        return firmware;
    }

    public static async Task<int> Download(Firmware firmware, string saveTo, CancellationToken cancellationToken)
    {
        var noncestatus = WebHelper.GenerateNonce();
        if (noncestatus != 200)
        {
            LogHelper.WriteLog($"Error: Could not generate Nonce. Status code: {noncestatus}");
            return -1;
        }

        var xmlbinaryInit = XmlHelper.GetXmlBinaryInit(firmware.Filename, firmware.Version, firmware.Region, firmware.Model_Type);
        var xmlbinaryData = await WebHelper.DownloadBinaryInit(xmlbinaryInit);
        var htmlstatus = xmlbinaryData.Item1;
        var xmlresponse = xmlbinaryData.Item2;
        var xmlstatus = GetXMLStatusCode(xmlresponse);
        if (htmlstatus == 200 && xmlstatus == 200)
            return await WebHelper.DownloadBinary(firmware.Path, firmware.Filename, saveTo, firmware.Size, cancellationToken);

        LogHelper.WriteLog($"Error: Could not send BinaryInform. Status code {htmlstatus}/{xmlstatus}");
        return -1;
    }
}
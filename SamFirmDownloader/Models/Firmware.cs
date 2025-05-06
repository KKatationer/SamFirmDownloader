namespace SamFirmDownloader.Models;

public struct Firmware
{
    public string Model;
    public string DisplayName;
    public string Version;
    public string OS;
    public string LastModified;
    public string Filename;
    public string Path;
    public string Size;
    public byte[] CRC;
    public string Model_Type;
    public string Region;
    public int BinaryNature;
    public string LogicValueFactory;
    public string LogicValueHome;
    public string Announce;
    public bool ConnectionError;
    public int FetchAttempts;
}
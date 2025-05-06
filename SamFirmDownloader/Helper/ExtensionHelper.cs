namespace SamFirmDownloader.Helper;

internal static class ExtensionHelper
{
    public static bool Compare(this byte[] arr1, byte[] arr2)
    {
        if (arr1.Length != arr2.Length)
            return false;

        for (int index = 0; index < arr1.Length; ++index)
        {
            if (arr1[index] != arr2[index])
                return false;
        }

        return true;
    }
}
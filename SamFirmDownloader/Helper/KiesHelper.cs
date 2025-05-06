using System.Security.Cryptography;
using System.Text;

namespace SamFirmDownloader.Helper;

public static class KiesHelper
{
    private const string KEY_1 = "vicopx7dqu06emacgpnpy8j8zwhduwlh";

    private const string KEY_2 = "9u7qab84rpc16gvk";

    private static byte[] GetFKey(int[] inp)
    {
        var key = string.Empty;

        for (int i = 0; i < 16; i++)
            key += KEY_1[inp[i]];
        key += KEY_2;

        return Encoding.UTF8.GetBytes(key);
    }

    public static byte[] DecryptNonce(string? inp)
    {
        if (string.IsNullOrEmpty(inp))
            return [];

        var key = Encoding.UTF8.GetBytes(KEY_1);
        var iv = key.Take(16).ToArray();

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        var bytes = Convert.FromBase64String(inp);
        var decryptor = aes.CreateDecryptor(key, iv);
        var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);

        return decrypted;
    }

    public static string GetAuth(byte[] nonce)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var keydata = nonce.Select(c => c % 16).ToArray();
        var fkey = GetFKey(keydata);
        var iv = fkey.Take(16).ToArray();
        var encryptor = aes.CreateEncryptor(fkey, iv);
        var auth = encryptor.TransformFinalBlock(nonce, 0, nonce.Length);

        return Convert.ToBase64String(auth);
    }
}
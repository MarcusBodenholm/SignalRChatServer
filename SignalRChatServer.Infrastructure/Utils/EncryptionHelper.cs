using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace SignalRChatServer.Infrastructure.Utils;
public static class EncryptionHelper
{
    private static readonly string _key;
    static EncryptionHelper()
    {
        _key = Environment.GetEnvironmentVariable("EncryptionKey") ?? throw new InvalidOperationException("Encryption key is missing in the configuration.");
    }
    public static string Encrypt(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(_key);
            aes.GenerateIV();
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (var msEncrypt = new MemoryStream())
            {
                msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }
                return Convert.ToBase64String(msEncrypt.ToArray());

            }
        }
    }
    public static string Decrypt(string cipherText)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_key);
                aes.Padding = PaddingMode.PKCS7;
                aes.IV = fullCipher.Take(aes.BlockSize / 8).ToArray();
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (var msDecrypt = new MemoryStream(fullCipher.Skip(aes.BlockSize / 8).ToArray()))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
        catch
        {
            return cipherText;
        }
    }
    public static bool IsBase64String(string base64)
    {
        Span<byte> buffer = new Span<byte>(new byte[base64.Length]);
        return base64.Length % 4 == 0 && Convert.TryFromBase64String(base64, buffer, out _);
    }
}

using System.Security.Cryptography;
using System.Text;

namespace YZH.Core.Stand.Helpers;

/// <summary>密码工具 - 兼容 Vol 框架的 AES 加密 + 提供 MD5/SHA256</summary>
public class PasswordHelper
{
    // Vol 框架的 IV 值
    private static byte[] Keys = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };

    private readonly string _aesKey;

    public PasswordHelper(string aesKey) => _aesKey = aesKey;

    /// <summary>AES 加密（Vol 兼容模式） - Vol 框架内部使用 Aes.Create() 但命名为 DES</summary>
    public string AesEncrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        byte[] rgbKey = Encoding.UTF8.GetBytes(_aesKey[..16]); // Key 取 16 位
        byte[] rgbIV = Keys;
        byte[] inputByteArray = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        using var mStream = new MemoryStream();
        using (var cStream = new CryptoStream(mStream, aes.CreateEncryptor(rgbKey, rgbIV), CryptoStreamMode.Write))
        {
            cStream.Write(inputByteArray, 0, inputByteArray.Length);
            cStream.FlushFinalBlock();
        }
        return Convert.ToBase64String(mStream.ToArray()).Replace('+', '_').Replace('/', '~');
    }

    /// <summary>验证密码（Vol 兼容）</summary>
    public bool VerifyAes(string plainText, string encryptedText) =>
        AesEncrypt(plainText) == encryptedText;

    /// <summary>为兼容保留：VerifyDes 别名</summary>
    public bool VerifyDes(string plainText, string encryptedText) =>
        VerifyAes(plainText, encryptedText);

    /// <summary>MD5 加密</summary>
    public static string Md5(string input)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>SHA256 加密</summary>
    public static string Sha256(string input)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>生成随机密码</summary>
    public static string NewRandomPassword(int length = 8)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        var random = Random.Shared;
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>生成 N 位随机数字码</summary>
    public static string NewRandomDigits(int length = 6)
    {
        var random = Random.Shared;
        return new string(Enumerable.Range(0, length)
            .Select(_ => (char)('0' + random.Next(10))).ToArray());
    }
}

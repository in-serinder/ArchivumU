using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ArchivumU.Models;

public class AES128EncHelperModel
{
        /// AES128 加密（CBC PKCS7）
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <param name="key16">16位密钥(128bit)</param>
    /// <param name="iv16">16位向量</param>
    /// <returns>Base64密文</returns>
    public static string Aes128Encrypt(string plainText, string key16, string iv16)
    {
        if (key16.Length != 16 || iv16.Length != 16)
            throw new ArgumentException("AES128密钥和IV必须是16个字符");

        byte[] keyBytes = Encoding.UTF8.GetBytes(key16);
        byte[] ivBytes = Encoding.UTF8.GetBytes(iv16);
        byte[] dataBytes = Encoding.UTF8.GetBytes(plainText);

        using Aes aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keyBytes;
        aes.IV = ivBytes;

        using ICryptoTransform encryptor = aes.CreateEncryptor();
        using MemoryStream ms = new MemoryStream();
        using CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        
        cs.Write(dataBytes, 0, dataBytes.Length);
        cs.FlushFinalBlock();

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// AES128 解密（CBC PKCS7）
    /// </summary>
    /// <param name="cipherBase64">Base64密文</param>
    /// <param name="key16">16位密钥</param>
    /// <param name="iv16">16位向量</param>
    /// <returns>明文</returns>
    public static string Aes128Decrypt(string cipherBase64, string key16, string iv16)
    {
        if (key16.Length != 16 || iv16.Length != 16)
            throw new ArgumentException("AES128密钥和IV必须是16个字符");

        byte[] keyBytes = Encoding.UTF8.GetBytes(key16);
        byte[] ivBytes = Encoding.UTF8.GetBytes(iv16);
        byte[] cipherBytes = Convert.FromBase64String(cipherBase64);

        using Aes aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = keyBytes;
        aes.IV = ivBytes;

        using ICryptoTransform decryptor = aes.CreateDecryptor();
        using MemoryStream ms = new MemoryStream(cipherBytes);
        using CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using StreamReader sr = new StreamReader(cs, Encoding.UTF8);

        return sr.ReadToEnd();
    }

    // 生成随机16位密钥/IV工具方法
    public static string Generate16KeyOrIv()
    {
        byte[] buf = new byte[16];
        RandomNumberGenerator.Fill(buf);
        // 转可见字符，也可直接使用byte数组
        return Convert.ToBase64String(buf).Substring(0, 16);
    }
}
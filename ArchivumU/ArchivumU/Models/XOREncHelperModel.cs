using System;

namespace ArchivumU.Models;

public class XOREncHelperModel
{
    /// <summary>
    /// XOR 加密/解密（同一方法两用，再次调用即可解密）
    /// </summary>
    /// <param name="inputText">原始明文 / 待解密密文</param>
    /// <param name="xorKey">自定义异或密钥（任意长度字符串）</param>
    /// <returns>Base64 编码结果，方便存储传输</returns>
    public string XorEncryptDecrypt(string inputText, string xorKey)
    {
        if (string.IsNullOrEmpty(inputText) || string.IsNullOrEmpty(xorKey))
            throw new ArgumentException("输入文本与异或密钥不能为空");

        // 文本转字节数组
        byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(inputText);
        byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(xorKey);
        byte[] resultBytes = new byte[inputBytes.Length];

        // 循环密钥逐字节异或
        for (int i = 0; i < inputBytes.Length; i++)
        {
            int keyIndex = i % keyBytes.Length;
            resultBytes[i] = (byte)(inputBytes[i] ^ keyBytes[keyIndex]);
        }

        // 返回Base64字符串，避免二进制乱码
        return Convert.ToBase64String(resultBytes);
    }

    /// <summary>
    /// 从Base64密文还原原始文本
    /// </summary>
    /// <param name="base64Cipher">XorEncryptDecrypt输出的Base64密文</param>
    /// <param name="xorKey">加密时使用的相同密钥</param>
    /// <returns>原始明文</returns>
    public string XorDecodeFromBase64(string base64Cipher, string xorKey)
    {
        if (string.IsNullOrEmpty(base64Cipher) || string.IsNullOrEmpty(xorKey))
            throw new ArgumentException("密文与异或密钥不能为空");

        byte[] cipherBytes = Convert.FromBase64String(base64Cipher);
        byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(xorKey);
        byte[] originBytes = new byte[cipherBytes.Length];

        for (int i = 0; i < cipherBytes.Length; i++)
        {
            int keyIndex = i % keyBytes.Length;
            originBytes[i] = (byte)(cipherBytes[i] ^ keyBytes[keyIndex]);
        }

        return System.Text.Encoding.UTF8.GetString(originBytes);
    }
}
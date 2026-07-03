using System;
using System.Text;

namespace ArchivumU.Models;

public class RC4EncHelperModel
{
      /// <summary>
    /// RC4 加密/解密（同一套逻辑，相同密钥即可还原）
    /// </summary>
    /// <param name="input">原始明文 / Base64密文解码后的二进制文本</param>
    /// <param name="key">自定义密钥，任意长度字符串</param>
    /// <returns>Base64编码密文，方便存储传输</returns>
    public string Encrypt(string input, string key)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(key))
            throw new ArgumentException("输入文本与密钥不可为空");

        byte[] data = Encoding.UTF8.GetBytes(input);
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] result = Rc4Transform(data, keyBytes);
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// RC4 解密
    /// </summary>
    /// <param name="base64Cipher">加密输出的Base64字符串</param>
    /// <param name="key">加密使用的密钥</param>
    /// <returns>原始明文</returns>
    public string Decrypt(string base64Cipher, string key)
    {
        if (string.IsNullOrEmpty(base64Cipher) || string.IsNullOrEmpty(key))
            throw new ArgumentException("密文与密钥不可为空");

        byte[] cipherBytes = Convert.FromBase64String(base64Cipher);
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] origin = Rc4Transform(cipherBytes, keyBytes);
        return Encoding.UTF8.GetString(origin);
    }

    /// <summary>
    /// RC4 核心算法变换（加解密共用）
    /// </summary>
    private byte[] Rc4Transform(byte[] data, byte[] key)
    {
        // 1. 初始化S盒
        byte[] sBox = new byte[256];
        for (int i = 0; i < 256; i++)
            sBox[i] = (byte)i;

        // 2. KSA 密钥调度算法
        int j = 0;
        for (int i = 0; i < 256; i++)
        {
            j = (j + sBox[i] + key[i % key.Length]) % 256;
            // 交换
            (sBox[i], sBox[j]) = (sBox[j], sBox[i]);
        }

        // 3. PRGA 伪随机生成流，逐字节异或
        byte[] output = new byte[data.Length];
        int x = 0, y = 0;
        for (int k = 0; k < data.Length; k++)
        {
            x = (x + 1) % 256;
            y = (y + sBox[x]) % 256;
            (sBox[x], sBox[y]) = (sBox[y], sBox[x]);

            int prgaIndex = (sBox[x] + sBox[y]) % 256;
            byte keystreamByte = sBox[prgaIndex];
            output[k] = (byte)(data[k] ^ keystreamByte);
        }

        return output;
    }
}
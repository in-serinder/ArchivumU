using System.Collections.Generic;
using System.Linq;

namespace ArchivumU.Models;

public class CaesarEncHelperModel
{
    /// <summary>
    /// 凯撒加密
    /// </summary>
    /// <param name="text">原始文本</param>
    /// <param name="shift">偏移量（正数右移加密，负数左移解密）</param>
    /// <returns>加密后字符串</returns>
    public string Encrypt(string text, int shift)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        char[] chars = text.ToCharArray();
        // 统一将偏移量缩小到 0~25 区间，避免超大偏移
        int realShift = shift % 26;
        if (realShift < 0) realShift += 26;

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            // 大写字母 A-Z
            if (char.IsUpper(c))
            {
                c = (char)(((c - 'A' + realShift) % 26) + 'A');
            }
            // 小写字母 a-z
            else if (char.IsLower(c))
            {
                c = (char)(((c - 'a' + realShift) % 26) + 'a');
            }
            // 数字、符号、中文、空格保持不变
            chars[i] = c;
        }
        return new string(chars);
    }

    /// <summary>
    /// 凯撒解密
    /// </summary>
    /// <param name="cipherText">凯撒密文</param>
    /// <param name="shift">加密时使用的偏移量</param>
    /// <returns>原始明文</returns>
    public string Decrypt(string cipherText, int shift)
    {
        // 解密等价于反向偏移
        return Encrypt(cipherText, -shift);
    }

    /// <summary>
    /// 暴力破解凯撒密码（遍历全部26种偏移输出所有结果）
    /// </summary>
    /// <param name="cipherText">密文</param>
    /// <returns>key=偏移量 value=对应文本</returns>
    public Dictionary<int, string> BruteForceDecrypt(string cipherText)
    {
        Dictionary<int, string> result = new();
        for (int i = 1; i <= 26; i++)
        {
            result.Add(i, Decrypt(cipherText, i));
        }
        return result;
    }
    
    /// <summary>
    /// </summary>
    /// <param name="cipherText">凯撒密文</param>
    /// <returns>正确偏移shift(0~25)；无有效字母返回-1</returns>
    public int GetRecoverShiftByCipherOnly(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
            return -1;

        // 统计小写字母出现频次
        Dictionary<char, int> freq = new();
        foreach (char c in cipherText.ToLower())
        {
            if (c is >= 'a' and <= 'z')
            {
                if (freq.ContainsKey(c))
                    freq[c]++;
                else
                    freq[c] = 1;
            }
        }

        // 无英文字母无法推算
        if (freq.Count == 0)
            return -1;

        // 取出现次数最多的字母
        char maxChar = freq.OrderByDescending(kv => kv.Value).First().Key;

        // 英文最高频字母 e(4)，计算偏移：密文字母 - e
        int shift = (maxChar - 'e' + 26) % 26;
        return shift;
    }
}
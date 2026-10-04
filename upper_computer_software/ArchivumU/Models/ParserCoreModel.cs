using System;
using System.Collections.Generic;

namespace ArchivumU.Models
{
    /// <summary>
    /// 所有解析结果对象的公共契约。
    /// 采用「独立 + 嵌套」两种接口风格：
    ///   - <see cref="IParserResult"/> 为顶层独立接口，统一描述一次解析的成败；
    ///   - 各 Parser_* 类内部再嵌套定义更强的强类型接口（如 IParserResult&lt;T&gt;）。
    /// 解析函数不直接与串口交互，只负责：字符串 -&gt; 参数 / 参数 -&gt; 字符串。
    /// </summary>
    public interface IParserResult
    {
        /// <summary>解析或操作是否成功。</summary>
        bool Success { get; }

        /// <summary>当 Success 为 false 时的说明信息。</summary>
        string Message { get; }
    }

    /// <summary>
    /// 带数据载荷的解析结果接口。
    /// </summary>
    /// <typeparam name="T">载荷类型。</typeparam>
    public interface IParserResult<T> : IParserResult
    {
        /// <summary>解析得到的强类型数据。</summary>
        T Data { get; }
    }

    /// <summary>
    /// 协议通用常量与基础工具。
    /// 说明：本工程已废弃 CMD.md 中 STATUS 相关的前缀/指令设计，
    /// 因此此处不再提供 STATUS 前缀的处理。
    /// </summary>
    public static class ParserProtocol
    {
        /// <summary>指令前缀。</summary>
        public const string CmdPrefix = "AT";

        /// <summary>参数分隔符。</summary>
        public const char Separator = '+';

        /// <summary>行结束符。</summary>
        public const string Crlf = "\r\n";

        /// <summary>一条指令最多被拆分的 token 数量（与固件 CMD_Parser 的 tokens[10] 一致）。</summary>
        public const int MaxTokens = 10;

        /// <summary>
        /// 将参数数组组合为一条完整 AT 指令字符串。
        /// 结果形如：AT+AUTH+PASSWORD+CREATE+xxx
        /// </summary>
        public static string Compose(params object[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return CmdPrefix;
            }

            var parts = new List<string>(tokens.Length + 1) { CmdPrefix };
            foreach (var t in tokens)
            {
                if (t == null)
                {
                    parts.Add(string.Empty);
                }
                else
                {
                    parts.Add(Convert.ToString(t));
                }
            }
            return string.Join(Separator.ToString(), parts);
        }

        /// <summary>
        /// 将设备回包拆分为 token 数组（按 '+' 拆分，去除首尾空白与 \r\n）。
        /// 注意：默认只按 '+' 拆分，键值内容中的 '|' 等由具体解析器处理。
        /// </summary>
        public static string[] Split(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return Array.Empty<string>();
            }

            string trimmed = response.Trim('\r', '\n', ' ', '\t');
            return trimmed.Split(Separator);
        }

        /// <summary>
        /// 校验回包是否以指定前缀开头（如 "AUTH"、"DATA"）。
        /// </summary>
        public static bool HasPrefix(string response, string prefix)
        {
            if (string.IsNullOrEmpty(response) || string.IsNullOrEmpty(prefix))
            {
                return false;
            }

            string trimmed = response.Trim('\r', '\n', ' ', '\t');
            return trimmed.StartsWith(prefix + Separator, StringComparison.Ordinal)
                   || trimmed.Equals(prefix, StringComparison.Ordinal)
                   || trimmed.StartsWith(prefix + "=", StringComparison.Ordinal);
        }
    }
}

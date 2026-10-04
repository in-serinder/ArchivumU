using System;

namespace ArchivumU.Models
{
    /// <summary>
    /// USAGE 指令类解析器（使用统计）。
    ///
    /// 发送（TX_）：把业务参数组合为 AT 指令字符串返回，不直接与串口交互。
    /// 接收（RX_）：把串口返回的字符串解析为强类型参数，通过接口返回。
    ///
    /// 协议（见 CMD.md 第 2.5 节）：
    ///   AT+USAGE -&gt; USAGE+&lt;总容量&gt;+&lt;已用&gt;+&lt;空闲&gt;+&lt;水位&gt;+&lt;下一地址&gt;+&lt;使用率&gt;
    /// </summary>
    public static class ParserUsage
    {
        // ============================ 嵌套强类型接口 ============================

        /// <summary>USAGE 类回包解析结果（载荷为 <see cref="UsageInfo"/>）。</summary>
        public interface IUsageParserResult : IParserResult<UsageInfo> { }

        /// <summary>使用统计深解析载荷（USAGE+...）。</summary>
        public sealed class UsageInfo
        {
            /// <summary>总容量。</summary>
            public long Total { get; set; }
            /// <summary>已用。</summary>
            public long Used { get; set; }
            /// <summary>空闲。</summary>
            public long Free { get; set; }
            /// <summary>水位。</summary>
            public long Watermark { get; set; }
            /// <summary>下一地址。</summary>
            public long NextAddress { get; set; }
            /// <summary>使用率原始文本（可能带 '%'）。</summary>
            public string UsageRateText { get; set; } = string.Empty;
            /// <summary>使用率百分比（解析后的数值，缺失时为 0）。</summary>
            public double UsageRate { get; set; }
        }

        // ============================ 内部实现 ============================

        private sealed class UsageParserResult : IUsageParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public UsageInfo Data { get; set; }
        }

        // ============================ 发送：TX_* ============================

        /// <summary>组合「使用统计」指令：AT+USAGE</summary>
        public static string TX_USAGE()
            => ParserProtocol.Compose("USAGE");

        // ============================ 接收：RX_* ============================

        /// <summary>
        /// 解析 USAGE 回包。
        /// USAGE+&lt;总容量&gt;+&lt;已用&gt;+&lt;空闲&gt;+&lt;水位&gt;+&lt;下一地址&gt;+&lt;使用率&gt;
        /// </summary>
        public static IUsageParserResult RX_USAGE(string response)
        {
            var result = new UsageParserResult { Success = false, Message = "无法解析 USAGE 回包" };

            if (!ParserProtocol.HasPrefix(response, "USAGE"))
            {
                return result;
            }

            var tokens = ParserProtocol.Split(response);
            if (tokens.Length < 2)
            {
                return result;
            }

            var usage = new UsageInfo();
            if (tokens.Length > 1) usage.Total = ParseLong(tokens[1]);
            if (tokens.Length > 2) usage.Used = ParseLong(tokens[2]);
            if (tokens.Length > 3) usage.Free = ParseLong(tokens[3]);
            if (tokens.Length > 4) usage.Watermark = ParseLong(tokens[4]);
            if (tokens.Length > 5) usage.NextAddress = ParseLong(tokens[5]);
            if (tokens.Length > 6)
            {
                usage.UsageRateText = tokens[6];
                usage.UsageRate = ParseRate(tokens[6]);
            }

            result.Data = usage;
            result.Success = true;
            result.Message = "OK";
            return result;
        }

        // ============================ 辅助数值解析 ============================

        private static long ParseLong(string s) => long.TryParse(s, out long v) ? v : 0L;

        private static double ParseRate(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0d;
            }
            string cleaned = s.Trim().TrimEnd('%');
            return double.TryParse(cleaned, out double v) ? v : 0d;
        }
    }
}

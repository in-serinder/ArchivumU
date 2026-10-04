using System;
using System.Collections.Generic;

namespace ArchivumU.Models
{
    /// <summary>
    /// MISC 指令类解析器（格式化 / 碎片整理 / 刷新 / 加密设置 等杂项）。
    ///
    /// 发送（TX_）：把业务参数组合为 AT 指令字符串返回，不直接与串口交互。
    /// 接收（RX_）：把串口返回的字符串解析为强类型参数，通过接口返回。
    ///
    /// 协议（见 CMD.md 第 6/7 节）：
    ///   AT+FORMAT+DEV                        -&gt; RESULT+0/1
    ///   AT+FORMAT+BLOCK+&lt;块标识&gt;+&lt;块ID或块名&gt; -&gt; RESULT+0/1
    ///   AT+DEFRAG                            -&gt; RESULT+0
    ///   AT+REFRESH                           -&gt; RESULT+0
    ///   AT+ENC+SET+&lt;算法&gt;                    -&gt; RESULT+0/ERR
    ///   AT+ENC+KEY+&lt;KEY_HEX&gt;                 -&gt; RESULT+0/ERR
    ///   AT+ENC+SALT+&lt;SALT_HEX&gt;               -&gt; RESULT+0/ERR
    ///   AT+ENC+KDF+&lt;KDF算法&gt;                  -&gt; RESULT+0/ERR
    ///
    /// 说明：本工程已废弃 CMD.md 中 STATUS 相关的前缀/指令设计，故此处不含 STATUS 处理。
    /// </summary>
    public static class Parser_MISC
    {
        // ============================ 嵌套强类型接口 ============================

        /// <summary>MISC 类回包解析结果（载荷为一句话描述，具体错误码见 Parser_ERR）。</summary>
        public interface IMiscParserResult : IParserResult<string> { }

        /// <summary>块标识（用于 FORMAT+BLOCK 的 block_flag 参数）。</summary>
        public enum BlockFlag
        {
            /// <summary>0 按块名。</summary>
            ByName = 0,
            /// <summary>1 按块 ID。</summary>
            ById = 1
        }

        /// <summary>KDF 算法枚举（见 CMD.md 第 7 节）。</summary>
        public enum KdfAlgorithm
        {
            /// <summary>0 NONE。</summary>
            None = 0,
            /// <summary>1 SHA256。</summary>
            Sha256 = 1,
            /// <summary>2 HKDF-SHA256。</summary>
            HkdfSha256 = 2,
            /// <summary>3 PBKDF2-SHA256。</summary>
            Pbkdf2Sha256 = 3
        }

        // ============================ 内部实现 ============================

        private sealed class MiscParserResult : IMiscParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Data { get; set; } = string.Empty;
        }

        // ============================ 发送：TX_* ============================

        /// <summary>组合「格式化设备」指令：AT+FORMAT+DEV</summary>
        public static string TX_FORMAT_DEV()
            => ParserProtocol.Compose("FORMAT", "DEV");

        /// <summary>组合「格式化块」指令：AT+FORMAT+BLOCK+&lt;块标识&gt;+&lt;块ID或块名&gt;</summary>
        public static string TX_FORMAT_BLOCK(BlockFlag flag, string blockIdentifier)
            => ParserProtocol.Compose("FORMAT", "BLOCK", (int)flag, blockIdentifier);

        /// <summary>组合「格式化块」指令（按块名）便捷重载。</summary>
        public static string TX_FORMAT_BLOCK_BY_NAME(string blockName)
            => TX_FORMAT_BLOCK(BlockFlag.ByName, blockName);

        /// <summary>组合「格式化块」指令（按块 ID）便捷重载。</summary>
        public static string TX_FORMAT_BLOCK_BY_ID(int blockId)
            => TX_FORMAT_BLOCK(BlockFlag.ById, blockId.ToString());

        /// <summary>组合「碎片整理」指令：AT+DEFRAG</summary>
        public static string TX_DEFRAG()
            => ParserProtocol.Compose("DEFRAG");

        /// <summary>组合「刷新」指令：AT+REFRESH</summary>
        public static string TX_REFRESH()
            => ParserProtocol.Compose("REFRESH");

        /// <summary>组合「设置加密算法」指令：AT+ENC+SET+&lt;算法&gt;</summary>
        public static string TX_ENC_SET(EncryptionAlgorithm algorithm)
            => ParserProtocol.Compose("ENC", "SET", (int)algorithm);

        /// <summary>组合「设置加密算法」指令的重载（直接传算法整数）。</summary>
        public static string TX_ENC_SET(int algorithm)
            => ParserProtocol.Compose("ENC", "SET", algorithm);

        /// <summary>组合「设置主密钥」指令：AT+ENC+KEY+&lt;KEY_HEX&gt;</summary>
        public static string TX_ENC_KEY(string keyHex)
            => ParserProtocol.Compose("ENC", "KEY", keyHex);

        /// <summary>组合「设置盐」指令：AT+ENC+SALT+&lt;SALT_HEX&gt;</summary>
        public static string TX_ENC_SALT(string saltHex)
            => ParserProtocol.Compose("ENC", "SALT", saltHex);

        /// <summary>组合「设置 KDF」指令：AT+ENC+KDF+&lt;KDF算法&gt;</summary>
        public static string TX_ENC_KDF(KdfAlgorithm kdf)
            => ParserProtocol.Compose("ENC", "KDF", (int)kdf);

        // ============================ 接收：RX_* ============================

        /// <summary>
        /// 解析 MISC 类回包（RESULT+0/1 或 ERR+&lt;code&gt;）。
        /// 成功返回描述字符串；失败时 Message 给出原因。
        /// </summary>
        public static IMiscParserResult RX_MISC(string response)
        {
            var result = new MiscParserResult { Success = false, Message = "无法解析 MISC 回包" };

            if (string.IsNullOrEmpty(response))
            {
                return result;
            }

            // RESULT 分支
            if (ParserProtocol.HasPrefix(response, "RESULT"))
            {
                var rr = Parser_RESULT.RX_RESULT(response);
                if (rr != null && rr.Success)
                {
                    result.Success = rr.Data.IsOk;
                    result.Data = rr.Data.Description;
                    result.Message = rr.Data.Description;
                }
                return result;
            }

            // ERR 分支
            if (ParserProtocol.HasPrefix(response, "ERR"))
            {
                var er = Parser_ERR.RX_ERR(response);
                if (er != null && er.Success)
                {
                    result.Success = false;
                    result.Data = er.Data.Description;
                    result.Message = er.Data.Description;
                }
                return result;
            }

            return result;
        }
    }
}

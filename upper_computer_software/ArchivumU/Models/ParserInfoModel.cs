using System;
using System.Collections.Generic;

namespace ArchivumU.Models
{
    /// <summary>
    /// INFO 指令类解析器（设备信息 / 版本 / 初始化 / 加密描述）。
    ///
    /// 发送（TX_）：把业务参数组合为 AT 指令字符串返回，不直接与串口交互。
    /// 接收（RX_）：把串口返回的字符串解析为强类型参数，通过接口返回。
    ///
    /// 协议（见 CMD.md 第 2 节 / 1.1 节）：
    ///   AT+INIT+&lt;设备名&gt;+&lt;密码|UPASS&gt;+&lt;加密算法&gt;+&lt;数据片容量代码&gt; -&gt; RESULT+0/1
    ///   AT+INFO   -&gt; INFO+&lt;设备名&gt;+&lt;固件版本&gt;+&lt;密码状态&gt;+&lt;接入计数&gt;+&lt;块数量&gt;+&lt;键值对数量&gt;+&lt;加密方式&gt;+&lt;存储总大小&gt;
    ///               未初始化时 -&gt; INIT=0+&lt;固件版本&gt;
    ///   AT+VERSION -&gt; VERSION+&lt;固件版本&gt;+&lt;协议版本&gt;+&lt;存储格式版本&gt;
    ///   AT+ENCINFO -&gt; ENCINFO+&lt;算法&gt;+&lt;模式&gt;+&lt;槽大小&gt;+&lt;IV长度&gt;+&lt;TAG长度&gt;
    /// </summary>
    public static class Parser_INFO
    {
        // ============================ 嵌套强类型接口 ============================

        /// <summary>INFO 类回包解析结果（载荷为 <see cref="DeviceInfo"/>）。</summary>
        public interface IInfoParserResult : IParserResult<DeviceInfo> { }

        /// <summary>VERSION 类回包解析结果（载荷为 <see cref="VersionInfo"/>）。</summary>
        public interface IVersionParserResult : IParserResult<VersionInfo> { }

        /// <summary>ENCINFO 类回包解析结果（载荷为 <see cref="EncInfo"/>）。</summary>
        public interface IEncInfoParserResult : IParserResult<EncInfo> { }

        /// <summary>密码状态。</summary>
        public enum PasswordState
        {
            /// <summary>已启用。</summary>
            Enabled,
            /// <summary>已禁用。</summary>
            Disabled
        }

        /// <summary>设备信息深解析载荷（INFO+...）。</summary>
        public sealed class DeviceInfo
        {
            /// <summary>设备名。</summary>
            public string Name { get; set; } = string.Empty;
            /// <summary>固件版本。</summary>
            public string FirmwareVersion { get; set; } = string.Empty;
            /// <summary>密码状态原始文本（ENABLED / DISABLED）。</summary>
            public string PasswordStateText { get; set; } = string.Empty;
            /// <summary>结构化的密码状态。</summary>
            public PasswordState PasswordState { get; set; }
            /// <summary>接入计数。</summary>
            public long AccessCount { get; set; }
            /// <summary>块数量。</summary>
            public int BlockCount { get; set; }
            /// <summary>键值对数量。</summary>
            public int KeyValueCount { get; set; }
            /// <summary>加密方式原始文本（NON / AES / XOR / CESAR / RC4）。</summary>
            public string EncryptionText { get; set; } = string.Empty;
            /// <summary>存储总大小（字节）。</summary>
            public long TotalSize { get; set; }
        }

        /// <summary>版本信息深解析载荷（VERSION+...）。</summary>
        public sealed class VersionInfo
        {
            /// <summary>固件版本。</summary>
            public string FirmwareVersion { get; set; } = string.Empty;
            /// <summary>协议版本。</summary>
            public string ProtocolVersion { get; set; } = string.Empty;
            /// <summary>存储格式版本。</summary>
            public string StorageFormatVersion { get; set; } = string.Empty;
        }

        /// <summary>加密描述深解析载荷（ENCINFO+...）。</summary>
        public sealed class EncInfo
        {
            /// <summary>算法。</summary>
            public string Algorithm { get; set; } = string.Empty;
            /// <summary>模式。</summary>
            public string Mode { get; set; } = string.Empty;
            /// <summary>槽大小。</summary>
            public int SlotSize { get; set; }
            /// <summary>IV 长度。</summary>
            public int IvLength { get; set; }
            /// <summary>TAG 长度。</summary>
            public int TagLength { get; set; }
        }

        // ============================ 内部实现 ============================

        private sealed class InfoParserResult : IInfoParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public DeviceInfo Data { get; set; }
        }

        private sealed class VersionParserResult : IVersionParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public VersionInfo Data { get; set; }
        }

        private sealed class EncInfoParserResult : IEncInfoParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public EncInfo Data { get; set; }
        }

        private static PasswordState ParsePasswordState(string text)
            => string.Equals(text, "ENABLED", StringComparison.OrdinalIgnoreCase)
                ? PasswordState.Enabled
                : PasswordState.Disabled;

        // ============================ 发送：TX_* ============================

        /// <summary>
        /// 组合「设备初始化」指令：AT+INIT+&lt;设备名&gt;+&lt;密码&gt;+&lt;加密算法&gt;+&lt;数据片容量代码&gt;
        /// </summary>
        /// <param name="deviceName">设备名（1~17 Byte）。</param>
        /// <param name="password">密码，不启用密码时传入 "UPASS"。</param>
        /// <param name="encryptType">加密算法枚举值（见 <see cref="EncryptionAlgorithm"/>）。</param>
        /// <param name="dataSizeCode">数据片容量代码（见 <see cref="DataSizeCode"/>）。</param>
        public static string TX_INIT(string deviceName, string password, EncryptionAlgorithm encryptType, DataSizeCode dataSizeCode)
            => ParserProtocol.Compose("INIT", deviceName, password, (int)encryptType, (int)dataSizeCode);

        /// <summary>组合「设备初始化」指令的重载（直接传算法/容量代码整数）。</summary>
        public static string TX_INIT(string deviceName, string password, int encryptType, int dataSizeCode)
            => ParserProtocol.Compose("INIT", deviceName, password, encryptType, dataSizeCode);

        /// <summary>组合「读取设备信息」指令：AT+INFO</summary>
        public static string TX_INFO()
            => ParserProtocol.Compose("INFO");

        /// <summary>组合「版本信息」指令：AT+VERSION</summary>
        public static string TX_VERSION()
            => ParserProtocol.Compose("VERSION");

        /// <summary>组合「加密描述」指令：AT+ENCINFO</summary>
        public static string TX_ENCINFO()
            => ParserProtocol.Compose("ENCINFO");

        /// <summary>组合「在线检测」指令：AT+ECHO</summary>
        public static string TX_ECHO()
            => ParserProtocol.Compose("ECHO");

        // ============================ 接收：RX_* ============================

        /// <summary>
        /// 解析 INFO 回包。
        /// 已初始化：INFO+设备名+固件版本+密码状态+接入计数+块数量+键值对数量+加密方式+存储总大小；
        /// 未初始化：INIT=0+&lt;固件版本&gt;（此时 Success=true，但 Data 仅含 FirmwareVersion）。
        /// </summary>
        public static IInfoParserResult RX_INFO(string response)
        {
            var result = new InfoParserResult { Success = false, Message = "无法解析 INFO 回包" };

            if (ParserProtocol.HasPrefix(response, "INIT=0") || ParserProtocol.HasPrefix(response, "INIT"))
            {
                // 未初始化：INIT=0+<固件版本>
                var initTokens = ParserProtocol.Split(response);
                // tokens[0] 形如 "INIT=0"
                string fw = initTokens.Length >= 2 ? initTokens[1] : string.Empty;
                result.Data = new DeviceInfo { FirmwareVersion = fw };
                result.Success = true;
                result.Message = "设备未初始化";
                return result;
            }

            if (!ParserProtocol.HasPrefix(response, "INFO"))
            {
                return result;
            }

            var tokens = ParserProtocol.Split(response);
            // 期望 1(前缀) + 8(字段) = 9
            if (tokens.Length < 2)
            {
                return result;
            }

            var info = new DeviceInfo();
            if (tokens.Length > 1) info.Name = tokens[1];
            if (tokens.Length > 2) info.FirmwareVersion = tokens[2];
            if (tokens.Length > 3)
            {
                info.PasswordStateText = tokens[3];
                info.PasswordState = ParsePasswordState(tokens[3]);
            }
            if (tokens.Length > 4) info.AccessCount = ParseLong(tokens[4]);
            if (tokens.Length > 5) info.BlockCount = ParseInt(tokens[5]);
            if (tokens.Length > 6) info.KeyValueCount = ParseInt(tokens[6]);
            if (tokens.Length > 7) info.EncryptionText = tokens[7];
            if (tokens.Length > 8) info.TotalSize = ParseLong(tokens[8]);

            result.Data = info;
            result.Success = true;
            result.Message = "OK";
            return result;
        }

        /// <summary>
        /// 解析 VERSION 回包（VERSION+&lt;固件版本&gt;+&lt;协议版本&gt;+&lt;存储格式版本&gt;）。
        /// </summary>
        public static IVersionParserResult RX_VERSION(string response)
        {
            var result = new VersionParserResult { Success = false, Message = "无法解析 VERSION 回包" };

            if (!ParserProtocol.HasPrefix(response, "VERSION"))
            {
                return result;
            }

            var tokens = ParserProtocol.Split(response);
            if (tokens.Length < 2)
            {
                return result;
            }

            var ver = new VersionInfo();
            if (tokens.Length > 1) ver.FirmwareVersion = tokens[1];
            if (tokens.Length > 2) ver.ProtocolVersion = tokens[2];
            if (tokens.Length > 3) ver.StorageFormatVersion = tokens[3];

            result.Data = ver;
            result.Success = true;
            result.Message = "OK";
            return result;
        }

        /// <summary>
        /// 解析 ENCINFO 回包（ENCINFO+&lt;算法&gt;+&lt;模式&gt;+&lt;槽大小&gt;+&lt;IV长度&gt;+&lt;TAG长度&gt;）。
        /// </summary>
        public static IEncInfoParserResult RX_ENCINFO(string response)
        {
            var result = new EncInfoParserResult { Success = false, Message = "无法解析 ENCINFO 回包" };

            if (!ParserProtocol.HasPrefix(response, "ENCINFO"))
            {
                return result;
            }

            var tokens = ParserProtocol.Split(response);
            if (tokens.Length < 2)
            {
                return result;
            }

            var enc = new EncInfo();
            if (tokens.Length > 1) enc.Algorithm = tokens[1];
            if (tokens.Length > 2) enc.Mode = tokens[2];
            if (tokens.Length > 3) enc.SlotSize = ParseInt(tokens[3]);
            if (tokens.Length > 4) enc.IvLength = ParseInt(tokens[4]);
            if (tokens.Length > 5) enc.TagLength = ParseInt(tokens[5]);

            result.Data = enc;
            result.Success = true;
            result.Message = "OK";
            return result;
        }

        // ============================ 辅助数值解析 ============================

        private static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;
        private static long ParseLong(string s) => long.TryParse(s, out long v) ? v : 0L;
    }
}

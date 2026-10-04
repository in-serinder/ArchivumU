using System;

namespace ArchivumU.Models
{
    /// <summary>
    /// ERR 指令类解析器（错误码解析）。
    ///
    /// 发送（TX_）：把业务参数组合为 AT 指令字符串返回，不直接与串口交互。
    /// 接收（RX_）：把串口返回的字符串解析为强类型参数，通过接口返回。
    ///
    /// 协议（见 CMD.md 第 1.2 节）：设备回包 <c>ERR+&lt;code&gt;</c>。
    /// </summary>

    public static class ParserErr
    {
        // ============================ 嵌套强类型接口 ============================

        /// <summary>
        /// ERR 类回包解析结果。成功数据为 <see cref="ErrState"/>。
        /// 注意：这里 Success 表示「回包本身被成功解析」，而非「设备未报错」。
        /// 判断设备是否报错请使用 <see cref="ErrState.IsError"/>。
        /// </summary>
        public interface IErrParserResult : IParserResult<ErrState> { }
        /// 设备错误码枚举（与固件 StorageMgr/CommandParser 的 ERR_* 宏一致）。
        /// </summary>
        public enum ErrCode
        {
            /// <summary>0 成功。</summary>
            Ok = 0,
            /// <summary>1 失败。</summary>
            Fail = 1,
            /// <summary>2 未设置密码。</summary>
            NoPass = 2,
            /// <summary>3 未验证。</summary>
            NotAuth = 3,
            /// <summary>4 参数错误。</summary>
            Param = 4,
            /// <summary>5 未知错误。</summary>
            Unknown = 5,
            /// <summary>6 数据片已满。</summary>
            DataFull = 6,
            /// <summary>7 数据片未初始化。</summary>
            DataNotInit = 7,
            /// <summary>8 分配位图满。</summary>
            BitmapFull = 8,
            /// <summary>9 块不存在。</summary>
            BlockNotFound = 9,
            /// <summary>10 键不存在。</summary>
            KeyNotFound = 10,
            /// <summary>11 块已存在。</summary>
            BlockExist = 11,
            /// <summary>12 键已存在。</summary>
            KeyExist = 12,
            /// <summary>13 数据片容量代码非法。</summary>
            SizeCodeInvalid = 13,
            /// <summary>14 只读锁。</summary>
            WriteProtect = 14,
            /// <summary>15 校验失败。</summary>
            CrcFail = 15,
            /// <summary>16 加密算法不支持。</summary>
            EncUnsupported = 16,
            /// <summary>17 超时。</summary>
            Timeout = 17
        }

        /// <summary>
        /// ERR 深解析结果载荷。
        /// </summary>
        public sealed class ErrState
        {
            /// <summary>原始错误码。</summary>
            public int RawCode { get; set; }

            /// <summary>结构化的错误码（未定义时落到 Unknown）。</summary>
            public ErrCode Code { get; set; }

            /// <summary>错误码的语义描述。</summary>
            public string Description { get; set; } = string.Empty;

            /// <summary>是否为错误（非 ERR_OK）。</summary>
            public bool IsError => Code != ErrCode.Ok;
        }

        // ============================ 内部实现 ============================

        /// <summary>IErrParserResult 的内部实现。</summary>
        private sealed class ErrParserResult : IErrParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public ErrState Data { get; set; }
        }

        /// <summary>把错误码转换为可读描述。</summary>
        public static string Describe(ErrCode code) => code switch
        {
            ErrCode.Ok => "成功",
            ErrCode.Fail => "失败",
            ErrCode.NoPass => "未设置密码",
            ErrCode.NotAuth => "未验证",
            ErrCode.Param => "参数错误",
            ErrCode.Unknown => "未知错误",
            ErrCode.DataFull => "数据片已满",
            ErrCode.DataNotInit => "数据片未初始化",
            ErrCode.BitmapFull => "分配位图满",
            ErrCode.BlockNotFound => "块不存在",
            ErrCode.KeyNotFound => "键不存在",
            ErrCode.BlockExist => "块已存在",
            ErrCode.KeyExist => "键已存在",
            ErrCode.SizeCodeInvalid => "数据片容量代码非法",
            ErrCode.WriteProtect => "只读锁",
            ErrCode.CrcFail => "校验失败",
            ErrCode.EncUnsupported => "加密算法不支持",
            ErrCode.Timeout => "超时",
            _ => "未知"
        };

        // ============================ 发送：TX_* ============================
        //
        // ERR 为设备单向回包类型，上位机不主动发送 ERR 指令；
        // 但个别协议允许上位机主动索要错误码（保留一个生成入口以便扩展）。

        /// <summary>
        /// 组合「查询错误码」指令：AT+ERR
        /// （设备端当前未强制实现，保留用于协议扩展 / 兼容测试）。
        /// </summary>
        public static string TX_ERR_QUERY()
            => ParserProtocol.Compose("ERR");

        // ============================ 接收：RX_* ============================

        /// <summary>
        /// 解析 ERR 回包（ERR+&lt;code&gt;）。
        /// </summary>
        public static IErrParserResult RX_ERR(string response)
        {
            var result = new ErrParserResult { Success = false, Message = "无法解析 ERR 回包" };

            if (!ParserProtocol.HasPrefix(response, "ERR"))
            {
                return result;
            }

            var tokens = ParserProtocol.Split(response);
            if (tokens.Length < 2)
            {
                return result;
            }

            if (!int.TryParse(tokens[1], out int code))
            {
                result.Message = $"ERR 错误码非法: {tokens[1]}";
                return result;
            }

            ErrCode codeEnum = Enum.IsDefined(typeof(ErrCode), code)
                ? (ErrCode)code
                : ErrCode.Unknown;

            var state = new ErrState
            {
                RawCode = code,
                Code = codeEnum,
                Description = Describe(codeEnum)
            };

            result.Data = state;
            result.Success = true;
            result.Message = state.Description;
            return result;
        }
    }
}
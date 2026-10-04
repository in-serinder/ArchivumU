using System;

namespace ArchivumU.Models
{
    /// <summary>
    /// RESULT 指令类解析器（通用操作结果 0 成功 / 1 失败）。
    ///
    /// 发送（TX_）：把业务参数组合为 AT 指令字符串返回，不直接与串口交互。
    /// 接收（RX_）：把串口返回的字符串解析为强类型参数，通过接口返回。
    ///
    /// 协议（见 CMD.md 第 1.1 节）：设备回包 <c>RESULT+&lt;0|1&gt;</c>。
    /// </summary>
    public static class Parser_RESULT
    {
        // ============================ 嵌套强类型接口 ============================

        /// <summary>
        /// RESULT 类回包解析结果。成功数据为 <see cref="ResultState"/>。
        /// 注意：这里 Success 表示「回包本身被成功解析」，而非「设备操作成功」。
        /// 判断设备操作结果请使用 <see cref="ResultState.IsOk"/>。
        /// </summary>
        public interface IResultParserResult : IParserResult<ResultState> { }

        /// <summary>
        /// RESULT 回包中的返回码（RESULT+n）。
        /// </summary>
        public enum ResultCode
        {
            /// <summary>0 成功。</summary>
            Ok = 0,
            /// <summary>1 失败。</summary>
            Fail = 1
        }

        /// <summary>
        /// RESULT 深解析结果载荷。
        /// </summary>
        public sealed class ResultState
        {
            /// <summary>原始返回码。</summary>
            public int RawCode { get; set; }

            /// <summary>结构化的返回码（非 0/1 时落到 Fail）。</summary>
            public ResultCode Code { get; set; }

            /// <summary>返回码的语义描述。</summary>
            public string Description { get; set; } = string.Empty;

            /// <summary>设备操作是否成功（RESULT_OK）。</summary>
            public bool IsOk => Code == ResultCode.Ok;
        }

        // ============================ 内部实现 ============================

        /// <summary>IResultParserResult 的内部实现。</summary>
        private sealed class ResultParserResult : IResultParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public ResultState Data { get; set; }
        }

        /// <summary>把返回码转换为可读描述。</summary>
        public static string Describe(ResultCode code) => code switch
        {
            ResultCode.Ok => "成功",
            ResultCode.Fail => "失败",
            _ => "未知"
        };

        // ============================ 发送：TX_* ============================
        //
        // RESULT 为设备单向回包类型，上位机不主动发送 RESULT 指令。

        // ============================ 接收：RX_* ============================

        /// <summary>
        /// 解析 RESULT 回包（RESULT+0 / RESULT+1）。
        /// </summary>
        public static IResultParserResult RX_RESULT(string response)
        {
            var result = new ResultParserResult { Success = false, Message = "无法解析 RESULT 回包" };

            if (!ParserProtocol.HasPrefix(response, "RESULT"))
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
                result.Message = $"RESULT 返回码非法: {tokens[1]}";
                return result;
            }

            ResultCode codeEnum = Enum.IsDefined(typeof(ResultCode), code)
                ? (ResultCode)code
                : ResultCode.Fail;

            var state = new ResultState
            {
                RawCode = code,
                Code = codeEnum,
                Description = Describe(codeEnum)
            };

            result.Data = state;
            result.Success = true; // 回包解析成功
            result.Message = state.Description;
            return result;
        }
    }
}

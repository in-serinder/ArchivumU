using System;
using System.Collections.Generic;
using System.Text;

namespace ArchivumU.Models
{
    /// <summary>
    /// DATA 指令类解析器（CURD：读取 / 写入 / 创建 / 删除 / 更新 / 全获取 / 批量）。
    ///
    /// 发送（TX_）：把业务参数组合为 AT 指令字符串返回，不直接与串口交互。
    /// 接收（RX_）：把串口返回的字符串解析为强类型参数，通过接口返回。
    ///
    /// 协议（见 CMD.md 第 4/5/8 节）。
    /// 键值对格式：&lt;KEY&gt;=&lt;VALUE&gt;；块之间以 '|' 分隔；
    /// 读数回包 DATA+[块名;块ID](键值对)|...（见 4.1）。
    /// </summary>
    public static class Parser_DATA
    {
        // ============================ 嵌套强类型接口 ============================

        /// <summary>DATA 回包解析结果（载荷为块列表）。</summary>
        public interface IDataParserResult : IParserResult<IReadOnlyList<DataBlock>> { }

        /// <summary>
        /// 块标识（用于 CREATE+KEY / DELETE+KEY 的 block_flag 参数，见 CMD.md 4.3/4.4）。
        /// </summary>
        public enum BlockFlag
        {
            /// <summary>0 按块名。</summary>
            ByName = 0,
            /// <summary>1 按块 ID。</summary>
            ById = 1
        }

        /// <summary>单个键值对。</summary>
        public sealed class KeyValue
        {
            /// <summary>键。</summary>
            public string Key { get; set; } = string.Empty;
            /// <summary>值。</summary>
            public string Value { get; set; } = string.Empty;
        }

        /// <summary>一个数据块。</summary>
        public sealed class DataBlock
        {
            /// <summary>块名。</summary>
            public string Name { get; set; } = string.Empty;
            /// <summary>块 ID。</summary>
            public int Id { get; set; }
            /// <summary>块内键值对。</summary>
            public List<KeyValue> Items { get; } = new List<KeyValue>();

            /// <summary>便捷索引：按 KEY 获取 VALUE（不存在返回 null）。</summary>
            public string this[string key]
            {
                get
                {
                    foreach (var kv in Items)
                    {
                        if (string.Equals(kv.Key, key, StringComparison.Ordinal))
                        {
                            return kv.Value;
                        }
                    }
                    return null;
                }
            }
        }

        // ============================ 内部实现 ============================

        private sealed class DataParserResult : IDataParserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public IReadOnlyList<DataBlock> Data { get; set; } = Array.Empty<DataBlock>();
        }

        // ============================ 发送：TX_* ============================

        /// <summary>组合「读取全部块」指令：AT+READ+BLOCK</summary>
        public static string TX_READ_BLOCK_ALL()
            => ParserProtocol.Compose("READ", "BLOCK");

        /// <summary>组合「读取指定块」指令：AT+READ+BLOCK+&lt;块ID&gt;</summary>
        public static string TX_READ_BLOCK(int blockId)
            => ParserProtocol.Compose("READ", "BLOCK", blockId);

        /// <summary>组合「按名读取块」指令：AT+READ+BLOCK+NAME+&lt;块名&gt;</summary>
        public static string TX_READ_BLOCK_NAME(string blockName)
            => ParserProtocol.Compose("READ", "BLOCK", "NAME", blockName);

        /// <summary>组合「读取键值」指令：AT+READ+KEY+&lt;块ID&gt;+&lt;KEY&gt;</summary>
        public static string TX_READ_KEY(int blockId, string key)
            => ParserProtocol.Compose("READ", "KEY", blockId, key);

        /// <summary>组合「写入」指令：AT+WRITE+&lt;块ID&gt;+&lt;KEY&gt;+&lt;VALUE&gt;</summary>
        public static string TX_WRITE(int blockId, string key, string value)
            => ParserProtocol.Compose("WRITE", blockId, key, value);

        /// <summary>组合「写入原始」指令：AT+WRITE+RAW+&lt;块ID&gt;+&lt;KEY&gt;+&lt;HEX&gt;</summary>
        public static string TX_WRITE_RAW(int blockId, string key, string hex)
            => ParserProtocol.Compose("WRITE", "RAW", blockId, key, hex);

        /// <summary>组合「创建块」指令：AT+CREATE+BLOCK+&lt;块名&gt;+&lt;块大小&gt;</summary>
        public static string TX_CREATE_BLOCK(string blockName, int blockSize)
            => ParserProtocol.Compose("CREATE", "BLOCK", blockName, blockSize);

        /// <summary>组合「创建块（默认大小 USIZE）」指令：AT+CREATE+BLOCK+&lt;块名&gt;+USIZE</summary>
        public static string TX_CREATE_BLOCK(string blockName)
            => ParserProtocol.Compose("CREATE", "BLOCK", blockName, "USIZE");

        /// <summary>组合「创建键值」指令：AT+CREATE+KEY+&lt;块标识&gt;+&lt;块ID或块名&gt;+&lt;KEY&gt;+&lt;VALUE&gt;</summary>
        public static string TX_CREATE_KEY(BlockFlag flag, string blockIdentifier, string key, string value)
            => ParserProtocol.Compose("CREATE", "KEY", (int)flag, blockIdentifier, key, value);

        /// <summary>组合「创建键值」指令（按块名）便捷重载。</summary>
        public static string TX_CREATE_KEY_BY_NAME(string blockName, string key, string value)
            => TX_CREATE_KEY(BlockFlag.ByName, blockName, key, value);

        /// <summary>组合「创建键值」指令（按块 ID）便捷重载。</summary>
        public static string TX_CREATE_KEY_BY_ID(int blockId, string key, string value)
            => TX_CREATE_KEY(BlockFlag.ById, blockId.ToString(), key, value);

        /// <summary>组合「删除块」指令：AT+DELETE+BLOCK+&lt;块ID&gt;</summary>
        public static string TX_DELETE_BLOCK(int blockId)
            => ParserProtocol.Compose("DELETE", "BLOCK", blockId);

        /// <summary>组合「按名删除块」指令：AT+DELETE+BLOCK+NAME+&lt;块名&gt;</summary>
        public static string TX_DELETE_BLOCK_NAME(string blockName)
            => ParserProtocol.Compose("DELETE", "BLOCK", "NAME", blockName);

        /// <summary>组合「删除键值」指令：AT+DELETE+KEY+&lt;块标识&gt;+&lt;块ID或块名&gt;+&lt;KEY&gt;</summary>
        public static string TX_DELETE_KEY(BlockFlag flag, string blockIdentifier, string key)
            => ParserProtocol.Compose("DELETE", "KEY", (int)flag, blockIdentifier, key);

        /// <summary>组合「更新块」指令：AT+UPDATE+BLOCK+&lt;块ID&gt;+&lt;新块名&gt;</summary>
        public static string TX_UPDATE_BLOCK(int blockId, string newName)
            => ParserProtocol.Compose("UPDATE", "BLOCK", blockId, newName);

        /// <summary>组合「更新键值」指令：AT+UPDATE+KEY+&lt;块ID&gt;+&lt;KEY&gt;+&lt;VALUE&gt;</summary>
        public static string TX_UPDATE_KEY(int blockId, string key, string value)
            => ParserProtocol.Compose("UPDATE", "KEY", blockId, key, value);

        /// <summary>组合「全获取块」指令：AT+GET+ALL+BLOCK</summary>
        public static string TX_GET_ALL_BLOCK()
            => ParserProtocol.Compose("GET", "ALL", "BLOCK");

        /// <summary>组合「全获取键」指令：AT+GET+ALL+KEY+&lt;块ID&gt;</summary>
        public static string TX_GET_ALL_KEY(int blockId)
            => ParserProtocol.Compose("GET", "ALL", "KEY", blockId);

        /// <summary>组合「获取容量」指令：AT+GET+SIZE</summary>
        public static string TX_GET_SIZE()
            => ParserProtocol.Compose("GET", "SIZE");

        /// <summary>
        /// 组合「批量写入」指令头：AT+BATCH+WRITE+&lt;块ID&gt;+&lt;COUNT&gt;
        /// （后续 COUNT 行 KEY=VALUE 由调用方另行发送）。
        /// </summary>
        public static string TX_BATCH_WRITE(int blockId, int count)
            => ParserProtocol.Compose("BATCH", "WRITE", blockId, count);

        /// <summary>
        /// 组合「批量删除」指令头：AT+BATCH+DELETE+&lt;块ID&gt;+&lt;COUNT&gt;
        /// （后续 COUNT 行 KEY 由调用方另行发送）。
        /// </summary>
        public static string TX_BATCH_DELETE(int blockId, int count)
            => ParserProtocol.Compose("BATCH", "DELETE", blockId, count);

        // ============================ 接收：RX_* ============================

        /// <summary>
        /// 解析 DATA 回包：DATA+[块名;块ID](k=v|...)...（块之间以 '|' 分隔）。
        /// </summary>
        public static IDataParserResult RX_DATA(string response)
        {
            var result = new DataParserResult { Success = false, Message = "无法解析 DATA 回包" };

            if (!ParserProtocol.HasPrefix(response, "DATA"))
            {
                return result;
            }

            string trimmed = response.Trim('\r', '\n', ' ', '\t');
            string body = trimmed.Substring("DATA+".Length);

            var blocks = new List<DataBlock>();
            foreach (var blockToken in SplitBlocks(body))
            {
                var block = ParseBlock(blockToken);
                if (block != null)
                {
                    blocks.Add(block);
                }
            }

            result.Data = blocks;
            result.Success = true;
            result.Message = "OK";
            return result;
        }

        /// <summary>
        /// 解析「获取容量」回包（GET+SIZE 无固定格式，按数值数组返回）。
        /// 载荷为 int 数组，调用方按协议顺序解释。
        /// </summary>
        public static IParserResult<IReadOnlyList<int>> RX_GET_SIZE(string response)
        {
            var result = new GenericResult<IReadOnlyList<int>> { Success = false, Message = "无法解析容量回包" };
            result.Data = Array.Empty<int>();

            if (string.IsNullOrEmpty(response))
            {
                return result;
            }

            var tokens = ParserProtocol.Split(response);
            var nums = new List<int>();
            for (int i = 1; i < tokens.Length; i++)
            {
                if (int.TryParse(tokens[i], out int v))
                {
                    nums.Add(v);
                }
            }

            result.Data = nums;
            result.Success = true;
            result.Message = "OK";
            return result;
        }

        // ============================ 解析辅助 ============================

        private static IEnumerable<string> SplitBlocks(string body)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool inParen = false;

            foreach (char c in body)
            {
                if (c == '(')
                {
                    inParen = true;
                    sb.Append(c);
                }
                else if (c == ')')
                {
                    inParen = false;
                    sb.Append(c);
                }
                else if (c == '|' && !inParen)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0)
            {
                parts.Add(sb.ToString());
            }
            return parts;
        }

        private static DataBlock ParseBlock(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            var block = new DataBlock();

            int lp = token.IndexOf('(');
            int rp = token.LastIndexOf(')');

            string header = lp >= 0 ? token.Substring(0, lp) : token;
            string content = (lp >= 0 && rp > lp) ? token.Substring(lp + 1, rp - lp - 1) : string.Empty;

            header = header.Trim('[', ']', ' ', '\t');
            int semi = header.IndexOf(';');
            if (semi >= 0)
            {
                block.Name = header.Substring(0, semi).Trim();
                block.Id = int.TryParse(header.Substring(semi + 1).Trim(), out int id) ? id : 0;
            }
            else
            {
                block.Name = header.Trim();
            }

            foreach (var pair in SplitPairs(content))
            {
                int eq = pair.IndexOf('=');
                if (eq >= 0)
                {
                    block.Items.Add(new KeyValue
                    {
                        Key = pair.Substring(0, eq),
                        Value = pair.Substring(eq + 1)
                    });
                }
                else if (pair.Length > 0)
                {
                    block.Items.Add(new KeyValue { Key = pair, Value = string.Empty });
                }
            }

            return block;
        }

        private static IEnumerable<string> SplitPairs(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return Array.Empty<string>();
            }
            return content.Split('|');
        }

        /// <summary>通用结果包装（内部使用）。</summary>
        private sealed class GenericResult<T> : IParserResult<T>
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public T Data { get; set; }
        }
    }
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ArchivumU.Models;
using ArchivumU.Services;
using ArchivumU.Views.Components;

namespace ArchivumU.ViewModels;

/// <summary>
/// 当前已连接设备的数据视图模型（单例）。
/// 承载「块列表 / 键值对列表」，并负责与设备的串口读写（读取、创建、删除）。
///
/// 块与键值以当前连接端口（<see cref="CurrentPort"/>）为准。
/// </summary>
public class DeviceDataViewModel : ViewModelBase
{
    // 全局唯一静态实例
    public static DeviceDataViewModel Instance { get; } = new DeviceDataViewModel();

    public I18nViewModel I18n => I18nViewModel.Instance;

    private readonly SerialObjectHelperModel _serial = new SerialObjectHelperModel();

    #region 存储容量（由上位机计算）

    private long _totalBytes;

    /// <summary>存储总容量（字节，来自 AT+INFO 的存储总大小字段）。</summary>
    public long TotalBytes
    {
        get => _totalBytes;
        private set
        {
            _totalBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalStorageText));
            OnPropertyChanged(nameof(UsagePercent));
            OnPropertyChanged(nameof(AvailableBytes));
            OnPropertyChanged(nameof(AvailableStorageText));
        }
    }

    private long _usedBytes;

    /// <summary>已用容量（字节，由全部键值对的键名+值长度累计得出）。</summary>
    public long UsedBytes
    {
        get => _usedBytes;
        private set
        {
            _usedBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsedStorageText));
            OnPropertyChanged(nameof(UsagePercent));
            OnPropertyChanged(nameof(AvailableBytes));
            OnPropertyChanged(nameof(AvailableStorageText));
        }
    }

    /// <summary>剩余容量（字节）。</summary>
    public long AvailableBytes => TotalBytes > UsedBytes ? TotalBytes - UsedBytes : 0;

    /// <summary>使用率（0~100，用于进度条）。</summary>
    public double UsagePercent
    {
        get
        {
            if (TotalBytes <= 0)
            {
                return 0;
            }
            double p = (double)UsedBytes / TotalBytes * 100.0;
            return p < 0 ? 0 : (p > 100 ? 100 : p);
        }
    }

    /// <summary>总容量显示文本（自动换算单位）。</summary>
    public string TotalStorageText => FormatSize(TotalBytes);

    /// <summary>已用容量显示文本（自动换算单位）。</summary>
    public string UsedStorageText => FormatSize(UsedBytes);

    /// <summary>剩余容量显示文本（自动换算单位）。</summary>
    public string AvailableStorageText => FormatSize(AvailableBytes);

    #endregion

    /// <summary>当前连接的端口号（未连接时为 null）。</summary>
    public string? CurrentPort { get; private set; }

    /// <summary>当前选中的块 ID（-1 表示未选中）。</summary>
    public int SelectedBlockId { get; set; } = -1;

    /// <summary>
    /// 块列表正在刷新（重建）时置为 true。
    /// 刷新期间的选中临时丢失不应清空 SelectedBlockId，以便刷新后恢复选中。
    /// </summary>
    public bool IsRefreshingBlocks { get; private set; }

    /// <summary>当前选中的块（未选中时为 null）。</summary>
    public BlockItem? SelectedBlock { get; set; }

    /// <summary>当前选中的键值对（未选中时为 null）。</summary>
    public KeyValueItem? SelectedKeyValue { get; set; }

    /// <summary>已连接的块列表。</summary>
    public ObservableCollection<BlockItem> BlockItems { get; } = new ObservableCollection<BlockItem>();

    /// <summary>当前选中块的键值对列表。</summary>
    public ObservableCollection<KeyValueItem> KeyValueItems { get; } = new ObservableCollection<KeyValueItem>();

    private DeviceDataViewModel() { }

    /// <summary>设置当前连接端口（连接成功时调用）。</summary>
    public void SetPort(string? portName)
    {
        CurrentPort = portName;
    }

    /// <summary>清空当前设备数据（断开连接时调用）。</summary>
    public void Clear()
    {
        CurrentPort = null;
        SelectedBlockId = -1;
        SelectedBlock = null;
        SelectedKeyValue = null;
        BlockItems.Clear();
        KeyValueItems.Clear();
        TotalBytes = 0;
        UsedBytes = 0;
    }

    #region 块操作

    /// <summary>从设备读取全部块（AT+GET+ALL+BLOCK）。</summary>
    public async Task<bool> RefreshBlocksAsync()
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) =
            await _serial.QuickSendCommand(CurrentPort, AT_CMDTXHelperModel.AT_GET_ALL_BLOCK());

        if (!ret)
        {
            return false;
        }

        // 解析回包：DATA+[块名;块ID](k=v|...)|...
        var parsed = Parser_DATA.RX_DATA(response);

        // 标记：刷新期间不动选中状态，刷新后由 UI 按块 ID 恢复选中
        IsRefreshingBlocks = true;
        try
        {
            BlockItems.Clear();

            if (parsed.Success && parsed.Data != null)
            {
                foreach (var block in parsed.Data)
                {
                    // 块占用大小 = 该块内所有键值对的「键名 + 值」字节数之和
                    long blockSize = 0;
                    foreach (var kv in block.Items)
                    {
                        blockSize += System.Text.Encoding.UTF8.GetByteCount(kv.Key ?? string.Empty);
                        blockSize += System.Text.Encoding.UTF8.GetByteCount(kv.Value ?? string.Empty);
                    }

                    BlockItems.Add(new BlockItem(
                        block.Name,
                        FormatAddress(block.Id),
                        block.Id,
                        block.Items.Count,
                        blockSize));
                }
            }
        }
        finally
        {
            IsRefreshingBlocks = false;
        }

        // 同步刷新存储容量统计
        await RefreshStorageAsync();

        return true;
    }

    /// <summary>在设备上创建块（AT+CREATE+BLOCK+&lt;块名&gt;+&lt;块大小&gt;）。</summary>
    public async Task<bool> CreateBlockAsync(string blockName, int blockSize)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            AT_CMDTXHelperModel.AT_CREATE_BLOCK(blockName, blockSize));

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            await RefreshBlocksAsync();
        }
        return ok;
    }

    /// <summary>删除指定块（AT+DELETE+BLOCK+&lt;块ID&gt;）。</summary>
    public async Task<bool> DeleteBlockAsync(int blockId)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            AT_CMDTXHelperModel.AT_DELETE_BLOCK(blockId));

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            // 若删除的正是当前选中的块，清空键值对列表与选中状态
            if (SelectedBlockId == blockId)
            {
                KeyValueItems.Clear();
                SelectedBlockId = -1;
                SelectedBlock = null;
                SelectedKeyValue = null;
            }

            await RefreshBlocksAsync();
        }
        return ok;
    }

    /// <summary>重命名块（AT+UPDATE+BLOCK+&lt;块ID&gt;+&lt;新块名&gt;）。</summary>
    public async Task<bool> RenameBlockAsync(int blockId, string newName)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            Models.Parser_DATA.TX_UPDATE_BLOCK(blockId, newName));

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            await RefreshBlocksAsync();
        }
        return ok;
    }

    /// <summary>低级格式化指定块（AT+FORMAT+BLOCK+&lt;标志&gt;+&lt;块ID或块名&gt;）。</summary>
    public async Task<bool> FormatBlockAsync(int blockId, string blockName)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            AT_CMDTXHelperModel.AT_FORMAT_BLOCK(1, blockId.ToString()));

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            // 格式化会清空块内键值，若为当前选中块则刷新键值对列表
            if (SelectedBlockId == blockId)
            {
                await RefreshKeysAsync(blockId);
            }

            await RefreshBlocksAsync();
        }
        return ok;
    }

    /// <summary>格式化整个设备（AT+FORMAT+DEV）。</summary>
    public async Task<bool> FormatDeviceAsync()
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            AT_CMDTXHelperModel.AT_FORMAT_DEV());

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            await RefreshBlocksAsync();
            KeyValueItems.Clear();
        }
        return ok;
    }

    #endregion

    #region 键值操作

    /// <summary>
    /// 读取指定块的全部键值对。
    /// 设备对 <c>AT+READ+BLOCK+&lt;块ID&gt;</c> 返回的是槽位值数组（无键名），
    /// 因此这里改用 <c>AT+READ+BLOCK</c>（读取全部块，返回 <c>[块名;块ID](键=值|...)</c>），
    /// 再从中挑出目标块，从而拿到键名。
    /// </summary>
    public async Task<bool> RefreshKeysAsync(int blockId)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) =
            await _serial.QuickSendCommand(CurrentPort, Parser_DATA.TX_READ_BLOCK_ALL());

        if (!ret)
        {
            return false;
        }

        var parsed = Parser_DATA.RX_DATA(response);
        KeyValueItems.Clear();

        if (parsed.Success && parsed.Data != null)
        {
            foreach (var block in parsed.Data)
            {
                if (block.Id != blockId)
                {
                    continue;
                }

                foreach (var kv in block.Items)
                {
                    KeyValueItems.Add(new KeyValueItem(
                        kv.Key, kv.Value, kv.Value?.Length ?? 0, FormatAddress(block.Id)));
                }
                break;
            }
        }

        return true;
    }

    /// <summary>创建键值对（AT+CREATE+KEY+&lt;标志&gt;+&lt;块ID&gt;+&lt;KEY&gt;+&lt;VALUE&gt;）。</summary>
    public async Task<bool> CreateKeyAsync(int blockId, string key, string value)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            AT_CMDTXHelperModel.AT_CREATE_KEY(1, blockId.ToString(), key, value));

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            await RefreshAfterKeyChangeAsync(blockId);
        }
        return ok;
    }

    /// <summary>更新键值对（AT+UPDATE+KEY+&lt;块ID&gt;+&lt;KEY&gt;+&lt;VALUE&gt;）。</summary>
    public async Task<bool> UpdateKeyAsync(int blockId, string key, string value)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            AT_CMDTXHelperModel.AT_UPDATE_KEY(blockId, key, value));

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            await RefreshAfterKeyChangeAsync(blockId);
        }
        return ok;
    }

    /// <summary>删除键值对（AT+DELETE+KEY+&lt;标志&gt;+&lt;块ID&gt;+&lt;KEY&gt;）。</summary>
    public async Task<bool> DeleteKeyAsync(int blockId, string key)
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return false;
        }

        var (ret, response) = await _serial.QuickSendCommand(
            CurrentPort,
            AT_CMDTXHelperModel.AT_DELETE_KEY(1, blockId.ToString(), key, string.Empty));

        bool ok = ret && IsSuccess(response);
        if (ok)
        {
            await RefreshAfterKeyChangeAsync(blockId);
        }
        return ok;
    }

    /// <summary>
    /// 键值对（创建/更新/删除）操作成功后的统一刷新：
    /// 1) 刷新键值对列表；
    /// 2) 刷新块列表（块内键数量、块占用大小随之变化）；
    /// 3) 刷新存储容量统计（已用空间、占比随之变化）。
    /// </summary>
    private async Task RefreshAfterKeyChangeAsync(int blockId)
    {
        await RefreshKeysAsync(blockId);
        await RefreshBlocksAsync();
    }

    #endregion

    #region 存储容量刷新

    /// <summary>
    /// 刷新存储容量：先取 INFO 得到总容量，
    /// 再读取全部块并「脱壳」累计所有键值对的字节数作为已用容量。
    /// </summary>
    public async Task<bool> RefreshStorageAsync()
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            TotalBytes = 0;
            UsedBytes = 0;
            return false;
        }

        // 1) 取总容量
        var (infoRet, infoResponse) =
            await _serial.QuickSendCommand(CurrentPort, AT_CMDTXHelperModel.AT_INFO());
        if (infoRet)
        {
            var info = AT_CMDRXPeaserHelperModel.ParseInfoResponse(infoResponse);
            if (info != null && info.TotalSize > 0)
            {
                TotalBytes = info.TotalSize;
            }
        }

        // 2) 读取全部块并计算已用容量
        var (ret, response) =
            await _serial.QuickSendCommand(CurrentPort, Parser_DATA.TX_READ_BLOCK_ALL());
        if (ret)
        {
            var parsed = Parser_DATA.RX_DATA(response);
            if (parsed.Success && parsed.Data != null)
            {
                long used = 0;
                foreach (var block in parsed.Data)
                {
                    // 块目录项本身占用：块名 + 固定开销
                    used += System.Text.Encoding.UTF8.GetByteCount(block.Name);
                    foreach (var kv in block.Items)
                    {
                        // 键名 + 值 + 分隔符/终止符等固定开销（近似）
                        used += System.Text.Encoding.UTF8.GetByteCount(kv.Key);
                        used += System.Text.Encoding.UTF8.GetByteCount(kv.Value);
                        used += 2; // 分隔符与终止符开销估算
                    }
                }
                UsedBytes = used;
                SyncStorageToDeviceList();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把本 VM 计算出的总容量/已用容量同步回设备列表项与配置（以当前端口为主键），
    /// 保证设备列表中的占用信息始终为最新。
    /// </summary>
    private void SyncStorageToDeviceList()
    {
        if (string.IsNullOrEmpty(CurrentPort))
        {
            return;
        }

        if (TotalBytes <= 0 && UsedBytes <= 0)
        {
            return;
        }

        DeviceListViewModel.Instance.UpdateStorage(CurrentPort, TotalBytes, UsedBytes);

        var config = ConfigJsonService.Instance.GetDeviceByPort(CurrentPort);
        if (config != null)
        {
            if (TotalBytes > 0)
            {
                config.Storage.TotalSize = TotalBytes;
            }
            config.Storage.UsedSize = UsedBytes;
            ConfigJsonService.Instance.UpdateDevice(config.Name, config);
        }
    }

    #endregion

    #region 辅助

    private static string FormatAddress(int blockId) => $"0x{blockId:X2}";

    /// <summary>把字节数格式化为易读字符串（B / KB / MB）。</summary>
    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.##} KB";
        }
        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }

    /// <summary>
    /// 依据设备回包判断命令是否执行成功。
    ///
    /// 设备「成功」时的各种回包前缀：
    ///   <c>RESULT+0</c>（创建键/删除/更新/格式化）、
    ///   <c>DATA+&lt;块ID&gt;</c>（创建块）、
    ///   <c>DATA+[块名;块ID](...)</c>（读取块）、
    ///   <c>DATA+OK</c>（初始化）、
    ///   <c>AUTH+0</c>（认证/密码操作）、
    ///   <c>INFO+...</c> / <c>STATUS+0</c> 等。
    /// 设备「失败」时的回包前缀：
    ///   <c>ERR+&lt;code&gt;</c>、结束标记 <c>\EOF</c>。
    ///
    /// 由于串口读取可能把「上一条命令的残留（如 ECHO / 旧回包）」一起读进来，
    /// 因此这里<b>优先以整段响应里出现的成功标记为准</b>，仅在完全没有成功标记时
    /// 才判定为失败，以最大程度兼容下位机的多种成功返回格式、避免误判。
    /// </summary>
    private static bool IsSuccess(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return false;
        }

        string s = response;

        // 1) 先看是否出现任一明确的「成功」标记 —— 只要出现即视为成功，
        //    即使响应中混入了残留的旧数据（如 ECHO、上一次的失败回包）。
        if (s.Contains("RESULT+0")
            || s.Contains("DATA+OK")
            || s.Contains("DATA+[")   // READ/BLOCK、GET+ALL+BLOCK 的块数据回包
            || s.Contains("AUTH+0"))
        {
            return true;
        }

        // 2) 创建块的返回为 DATA+<块ID>（单个数字）
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"DATA\+\d+"))
        {
            return true;
        }

        // 3) 其余带结果前缀的回包（RESULT+/DATA+/AUTH+）且不含失败标记时，视为成功
        bool hasResultPrefix = s.Contains("RESULT+") || s.Contains("DATA+") || s.Contains("AUTH+");
        bool hasFailure = s.Contains("ERR+") || s.Contains("EOF");

        return hasResultPrefix && !hasFailure;
    }

    #endregion
}

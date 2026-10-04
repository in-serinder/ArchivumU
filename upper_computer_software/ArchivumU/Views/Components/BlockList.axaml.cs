using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ArchivumU.Models;
using ArchivumU.ViewModels;
using ArchivumU.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArchivumU.Views.Components;

public partial class BlockList : UserControl
{
    private feature_string feature_string = new feature_string();

    // 当前选中的块
    private BlockItem? _selectedBlock;

    public BlockList()
    {
        InitializeComponent();
        // 绑定到全局共享的「当前设备数据」视图模型
        DataContext = DeviceDataViewModel.Instance;
        BlockItems.CollectionChanged += (_, _) =>
        {
            UpdateEmptyPlaceholder();
            RestoreSelection();
        };
        UpdateEmptyPlaceholder();
    }

    // 兼容旧引用的集合（转发到共享实例）
    public ObservableCollection<BlockItem> BlockItems => DeviceDataViewModel.Instance.BlockItems;

    /// <summary>
    /// 块列表刷新（重建）后，按块 ID 恢复原先的选中项，
    /// 避免刷新导致选中丢失、进而清空键值对列表。
    /// </summary>
    private void RestoreSelection()
    {
        int targetId = DeviceDataViewModel.Instance.SelectedBlockId;
        if (targetId < 0)
        {
            return;
        }

        foreach (var b in BlockItems)
        {
            if (b.Id == targetId && !ReferenceEquals(LBBlockList.SelectedItem, b))
            {
                LBBlockList.SelectedItem = b;
                return;
            }
        }
    }

    /// <summary>刷新块列表（供菜单「刷新块」调用）。</summary>
    public async void RefreshBlocks()
    {
        if (string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort))
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.NotArchivumUDevice);
            return;
        }

        await DeviceDataViewModel.Instance.RefreshBlocksAsync();
    }

    /// <summary>弹出创建块对话框并执行（供菜单「创建块」调用）。</summary>
    public async void AddBlockDialog()
    {
        await CreateBlockInternalAsync();
    }

    /// <summary>删除当前选中块（供菜单「删除块」调用）。</summary>
    public async void DeleteSelectedBlock()
    {
        if (_selectedBlock == null)
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.PleaseSelectBlock);
            return;
        }

        await DeviceDataViewModel.Instance.DeleteBlockAsync(_selectedBlock.Id);
        _selectedBlock = null;
        DeviceDataViewModel.Instance.SelectedBlock = null;
        DeviceDataViewModel.Instance.SelectedBlockId = -1;
    }

    private void UpdateEmptyPlaceholder()
    {
        if (EmptyPlaceholder != null)
        {
            EmptyPlaceholder.IsVisible = BlockItems.Count == 0;
        }
    }

    // 选中块变化：联动键值对读取
    private async void LBBlockList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LBBlockList.SelectedItem is BlockItem block)
        {
            // 若选中的仍是同一个块（例如列表刷新后恢复选中），无需重复读取键值对
            bool sameBlock = DeviceDataViewModel.Instance.SelectedBlockId == block.Id;
            _selectedBlock = block;
            DeviceDataViewModel.Instance.SelectedBlockId = block.Id;
            DeviceDataViewModel.Instance.SelectedBlock = block;

            if (sameBlock)
            {
                return;
            }

            // 读取该块内的键值对
            if (!string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort))
            {
                await DeviceDataViewModel.Instance.RefreshKeysAsync(block.Id);
            }
        }
        else
        {
            // 块列表正在刷新（重建）期间的选中临时丢失：保留选中 ID，刷新后恢复。
            if (DeviceDataViewModel.Instance.IsRefreshingBlocks)
            {
                return;
            }

            // 列表真正清空（如切换设备/断开）时才清空选中
            if (BlockItems.Count == 0)
            {
                _selectedBlock = null;
                DeviceDataViewModel.Instance.SelectedBlockId = -1;
                DeviceDataViewModel.Instance.SelectedBlock = null;
            }
        }
    }

    /// <summary>重命名当前选中块（供菜单调用）。</summary>
    public async void RenameSelectedBlock()
    {
        var block = DeviceDataViewModel.Instance.SelectedBlock;
        if (block == null)
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.PleaseSelectBlock);
            return;
        }

        var result = await DialogMgrModel.ShowDialogAsync<BlockAddResult>(
            new BlockAddWindow(new BlockAddViewModel { Title = I18nViewModel.Instance.BlockRename }));

        if (result == null)
        {
            return;
        }

        await DeviceDataViewModel.Instance.RenameBlockAsync(block.Id, result.BlockName);
    }

    /// <summary>低级格式化当前选中块（供菜单调用）。</summary>
    public async void FormatSelectedBlock()
    {
        var block = DeviceDataViewModel.Instance.SelectedBlock;
        if (block == null)
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.PleaseSelectBlock);
            return;
        }

        await DeviceDataViewModel.Instance.FormatBlockAsync(block.Id, block.Name);
    }

    // 添加块：弹出 BlockAddWindow 收集参数，再联动串口创建
    private async void BTNBlockCreate_OnClick(object? sender, RoutedEventArgs e)
    {
        await CreateBlockInternalAsync();
    }

    private async Task CreateBlockInternalAsync()
    {
        if (string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort))
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.NotArchivumUDevice);
            return;
        }

        var result = await DialogMgrModel.ShowDialogAsync<BlockAddResult>(
            new BlockAddWindow(new BlockAddViewModel()));

        if (result == null)
        {
            return;
        }

        bool ok = await DeviceDataViewModel.Instance.CreateBlockAsync(result.BlockName, result.BlockSize);

        InfoDialogViewModel.Show(
            ok ? InfoDialogViewModel.InfoType.Success : InfoDialogViewModel.InfoType.Error,
            ok ? I18nViewModel.Instance.BlockAddSuccess : I18nViewModel.Instance.BlockAddFailed,
            $"{result.BlockName} ({result.BlockSize} Byte)");
    }

    // 删除选中块
    private async void BTNBlockDelete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedBlock == null)
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.PleaseSelectBlock);
            return;
        }

        bool ok = await DeviceDataViewModel.Instance.DeleteBlockAsync(_selectedBlock.Id);

        InfoDialogViewModel.Show(
            ok ? InfoDialogViewModel.InfoType.Success : InfoDialogViewModel.InfoType.Error,
            ok ? I18nViewModel.Instance.BlockDelSuccess : I18nViewModel.Instance.BlockDelFailed,
            _selectedBlock.Name);

        if (ok)
        {
            _selectedBlock = null;
        }
    }
}

public class BlockItem
{
    public string Name { get; set; }
    public string Address { get; set; } // 块地址
    public int Id { get; set; } // 块ID
    public int KeyNumber { get; set; } // 键值对数量
    public long Size { get; set; }

    public string SizeString => feature_string.formatSizeToString(Size);

    public BlockItem()
    {
    }

    public BlockItem(string name, string address, int id, int keyNumber, long size)
    {
        Name = name;
        Address = address;
        Id = id;
        KeyNumber = keyNumber;
        Size = size;
    }
}

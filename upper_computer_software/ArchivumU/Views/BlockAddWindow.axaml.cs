using ArchivumU.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArchivumU.Views;

/// <summary>
/// 添加块窗口：收集「块名 / 块大小」，确认后以 <see cref="BlockAddResult"/> 作为对话框返回值。
/// </summary>
public partial class BlockAddWindow : Window
{
    /// <summary>强类型获取视图模型。</summary>
    public BlockAddViewModel? Vm => DataContext as BlockAddViewModel;

    // 设计器 / 无参构造
    public BlockAddWindow()
    {
        InitializeComponent();
        DataContext = new BlockAddViewModel();
    }

    // 业务构造：传入设备约束
    public BlockAddWindow(BlockAddViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // 便捷构造
    public BlockAddWindow(int nameMaxLength, int sizeMax)
    {
        InitializeComponent();
        DataContext = new BlockAddViewModel(nameMaxLength, sizeMax);
    }

    private void BTNBlockAddCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void BTNBlockAddConfirm_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || !Vm.CanConfirm)
        {
            return;
        }

        Close(new BlockAddResult(Vm.BlockName, Vm.BlockSize));
    }
}

/// <summary>添加块对话框的返回值。</summary>
public sealed class BlockAddResult
{
    /// <summary>块名。</summary>
    public string BlockName { get; }

    /// <summary>块大小（字节）。</summary>
    public int BlockSize { get; }

    public BlockAddResult(string blockName, int blockSize)
    {
        BlockName = blockName;
        BlockSize = blockSize;
    }
}

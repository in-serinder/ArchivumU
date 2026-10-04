using ArchivumU.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArchivumU.Views;

/// <summary>
/// 添加键值对窗口：收集「键 / 值」，确认后以 <see cref="KeyAddResult"/> 作为对话框返回值。
/// </summary>
public partial class KeyAddWindow : Window
{
    public KeyAddViewModel? Vm => DataContext as KeyAddViewModel;

    public KeyAddWindow()
    {
        InitializeComponent();
        DataContext = new KeyAddViewModel();
    }

    public KeyAddWindow(KeyAddViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void BTNKeyAddCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void BTNKeyAddConfirm_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || !Vm.CanConfirm)
        {
            return;
        }

        Close(new KeyAddResult(Vm.Key, Vm.Value));
    }
}

/// <summary>添加键值对对话框的返回值。</summary>
public sealed class KeyAddResult
{
    public string Key { get; }
    public string Value { get; }

    public KeyAddResult(string key, string value)
    {
        Key = key;
        Value = value;
    }
}

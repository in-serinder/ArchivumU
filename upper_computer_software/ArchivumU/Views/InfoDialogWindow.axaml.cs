using ArchivumU.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArchivumU.Views;

public partial class InfoDialogWindow : Window
{

    // 设计器无参构造
    public InfoDialogWindow()
    {
        InitializeComponent();
        DataContext = new InfoDialogViewModel();
    }

    // 接收独立VM，隔离每个弹窗数据
    public InfoDialogWindow(InfoDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void BTNIDOK_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
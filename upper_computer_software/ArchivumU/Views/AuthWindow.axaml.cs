using ArchivumU.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ArchivumU.ViewModels;
using Avalonia.Interactivity;

namespace ArchivumU.Views;

public partial class AuthWindow : Window
{
    
    public string Device { get; set; }
    public string PortName { get; set; }

    public AuthWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        TBAuthOBJ.Text = $"{Device}@{PortName}";
    }
    
    
    
    



    // 带参构造，外部传参入口
    public AuthWindow(string device, string portName)
    {
        InitializeComponent();
        Device = device;
        PortName = portName;
        TBAuthOBJ.Text = $"{Device}@{PortName}";
        var mainVm = DialogMgrModel.GetMainViewModel<MainWindowViewModel>();
        DataContext = mainVm;
    }


    private void BTNAuthWinClose_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
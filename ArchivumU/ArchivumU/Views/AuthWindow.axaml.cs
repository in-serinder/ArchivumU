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
    public AuthWindow(MainWindowViewModel viewModel, string device, string portName)
    {
        InitializeComponent();
        DataContext = viewModel;
        Device = device;
        PortName = portName;
        TBAuthOBJ.Text = $"{Device}@{PortName}";
    }


    private void BTNAuthWinClose_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
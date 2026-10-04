using ArchivumU.Models;
using ArchivumU.Services;
using ArchivumU.ViewModels;
using Avalonia.Controls;
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
        // 取消认证：返回 false
        Close(false);
    }

    private async void BTNAuthWinConfirm_OnClick(object? sender, RoutedEventArgs e)
    {
        string password = PasswordInput.Text ?? string.Empty;
        if (string.IsNullOrEmpty(password))
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.Password);
            return;
        }

        // 联动串口进行密码校验
        bool ok = await DeviceConnectionService.VerifyPasswordAsync(PortName, password);

        if (ok)
        {
            Close(true);
        }
        else
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error,
                I18nViewModel.Instance.AuthenticationFailed,
                I18nViewModel.Instance.AuthenticationFailed);
        }
    }
}
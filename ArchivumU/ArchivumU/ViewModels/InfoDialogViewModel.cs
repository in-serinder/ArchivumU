using ArchivumU.Views;

namespace ArchivumU.ViewModels;

public class InfoDialogViewModel : ViewModelBase
{
    // 静态快捷
    public static InfoDialogViewModel Instance { get; } = new InfoDialogViewModel();

    public I18nViewModel I18n => I18nViewModel.Instance;

    public enum InfoType
    {
        Success,
        Warning,
        Error,
        Info
    }

    #region 带属性通知的绑定字段
    private InfoType _infoStatus = InfoType.Info;
    public InfoType InfoStatus
    {
        get => _infoStatus;
        set { _infoStatus = value; OnPropertyChanged(); }
    }

    private string _infoMessage = string.Empty;
    public string InfoMessage
    {
        get => _infoMessage;
        set { _infoMessage = value; OnPropertyChanged(); }
    }

    private string _infoText = string.Empty;
    public string InfoText
    {
        get => _infoText;
        set { _infoText = value; OnPropertyChanged(); }
    }

    private string _infoIcon = "../Assets/info.svg";
    public string InfoIcon
    {
        get => _infoIcon;
        set { _infoIcon = value; OnPropertyChanged(); }
    }
    #endregion

    public InfoDialogViewModel() { }

    /// <summary>
    /// 静态全局快捷调用（推荐业务使用）
    /// </summary>
    public static void Show(InfoType type, string message, string text)
    {
        // 每次弹窗新建独立VM，隔离数据
        var vm = new InfoDialogViewModel();
        vm.InfoStatus = type;
        vm.InfoMessage = message;
        vm.InfoText = text;
        vm.SwitchIcon();

        var window = new InfoDialogWindow();
        window.DataContext = vm;
        window.Show();
    }

    /// <summary>
    /// 内部切换图标
    /// </summary>
    public void SwitchIcon()
    {
        InfoIcon = InfoStatus switch
        {
            InfoType.Success => "../Assets/ok.svg",
            InfoType.Warning => "../Assets/warn.svg",
            InfoType.Error => "../Assets/error.svg",
            InfoType.Info => "../Assets/info.svg",
            _ => "../Assets/info.svg"
        };
    }
}
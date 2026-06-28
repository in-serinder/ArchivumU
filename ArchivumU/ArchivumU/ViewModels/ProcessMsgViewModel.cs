using System.ComponentModel;
using System.Diagnostics;

namespace ArchivumU.ViewModels;

public class ProcessMsgViewModel : ViewModelBase
{
    public I18nViewModel I18n => I18nViewModel.Instance;

    public enum Status
    {
        Processing,
        Done,
        Failed,
    }

    #region 带通知绑定属性
    private string _processCurTask = "测试任务";
    public string Process_Cur_Task
    {
        get => _processCurTask;
        set { _processCurTask = value; OnPropertyChanged(); }
    }

    private Status _processCurStatus = Status.Processing;
    public Status Process_Cur_Status
    {
        get => _processCurStatus;
        set
        {
            _processCurStatus = value;
            OnPropertyChanged();
            // 状态变更自动切换图标
            ProcessMsgIconSwitch();
        }
    }

    private int _processCurValue = 12;
    public int Process_Cur_Value
    {
        get => _processCurValue;
        set { _processCurValue = value; OnPropertyChanged(); }
    }

    private string _processCurIcon = string.Empty;
    public string Process_Cur_Icon
    {
        get => _processCurIcon;
        set { _processCurIcon = value; OnPropertyChanged(); }
    }
    #endregion

    // 真正静态单例（全局快捷入口，不频繁new）
    public static ProcessMsgViewModel Instance { get; } = new ProcessMsgViewModel();

    // 无参构造
    public ProcessMsgViewModel()
    {
        ProcessMsgIconSwitch();
        Debug.WriteLine($"ProcessMsgViewModel {Process_Cur_Icon}");
    }

    // 有参构造：创建独立弹窗专用
    public ProcessMsgViewModel(Status status, string msg)
    {
        Process_Cur_Status = status;
        Process_Cur_Task = msg;
    }

    /// <summary>
    /// 根据状态切换图标路径
    /// </summary>
    public void ProcessMsgIconSwitch()
    {
        Process_Cur_Icon = Process_Cur_Status switch
        {
            Status.Processing => "../Assets/process.svg",
            Status.Done => "../Assets/done.svg",
            Status.Failed => "../Assets/failed.svg",
            _ => "../Assets/process.svg"
        };
    }

    /// <summary>
    /// 更新进程窗口信息（外部调用）
    /// </summary>
    public void SetProcessMsgWindow(Status status, string msg, int processValue)
    {
        Process_Cur_Status = status;
        Process_Cur_Task = msg;
        Process_Cur_Value = processValue;
    }

    #region 静态快捷调用（推荐，每次弹窗独立VM互不干扰）
    /// <summary>
    /// 快速打开独立进程提示窗口
    /// </summary>
    public static void ShowProcessWindow(Status status, string msg, int value = 0)
    {
        var vm = new ProcessMsgViewModel(status, msg);
        if (value != 0)
            vm.Process_Cur_Value = value;

        // 这里替换成你对应的窗口，示例：
        // var win = new ProcessMsgWindow(vm);
        // win.Show();
    }
    #endregion
}
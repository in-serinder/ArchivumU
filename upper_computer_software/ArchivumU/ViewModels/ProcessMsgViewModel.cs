using System.ComponentModel;
using System.Diagnostics;
using ArchivumU.Views; // 引入窗口

namespace ArchivumU.ViewModels;

public class ProcessMsgViewModel : ViewModelBase
{
    // 静态单例
    public static ProcessMsgViewModel Instance { get; } = new ProcessMsgViewModel();

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
    /// 更新全局单例进度信息（常驻进度条用）
    /// </summary>
    public void SetProcessMsgWindow(Status status, string msg, int processValue)
    {
        Process_Cur_Status = status;
        Process_Cur_Task = msg;
        Process_Cur_Value = processValue;
    }

    #region 静态快捷调用弹窗
    public static void ShowProcessWindow(Status status, string msg, int value = 0)
    {
        var vm = new ProcessMsgViewModel(status, msg);
        if (value != 0)
            vm.Process_Cur_Value = value;

        var win = new ProcessDialogWindow(vm);
        win.Show();
        // 如需阻塞模态弹窗替换为：
        // win.ShowDialog(App.Current.MainWindow);
    }
    #endregion
}
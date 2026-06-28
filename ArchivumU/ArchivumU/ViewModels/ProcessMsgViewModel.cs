using System.ComponentModel;
using System.Diagnostics;

namespace ArchivumU.ViewModels;

public class ProcessMsgViewModel : ViewModelBase
{
    public I18nViewModel I18n => I18nViewModel.Instance;
    
    //全局单例
    public static ProcessMsgViewModel Instance => new ProcessMsgViewModel();

    public enum Status
    {
        Processing,
        Done,
        Failed,
    }
    

    public string Process_Cur_Task {get;set;} = "测试任务";
    public Status Process_Cur_Status {get;set;} = Status.Processing;
    public int Process_Cur_Value {get;set;} = 12;
    // public ProcessTypeIcon Process_Cur_Icon {get;set;} = ProcessTypeIcon.FAILED;
    public string Process_Cur_Icon {get;set;} = string.Empty;

    
    public ProcessMsgViewModel()
    {
        Debug.WriteLine($"ProcessMsgViewModel {Process_Cur_Icon}");
        ProcessMsgIconSwitch();
    }
    
    //有参传构造
    public ProcessMsgViewModel(Status status,string Msg)
    {
        Process_Cur_Status = status;
        ProcessMsgIconSwitch();
        Process_Cur_Task = Msg;
    }
    
    
    
    
    public void ProcessMsgIconSwitch()
    {


        switch (Process_Cur_Status)
        {
            case Status.Processing:
                Process_Cur_Icon = "../Assets/process.svg";
                break;
            case Status.Done:
                Process_Cur_Icon = "../Assets/done.svg";
                break;
            case Status.Failed:
                Process_Cur_Icon = "../Assets/failed.svg";
                break;
        }
    }
    
    //外部调窗口修改信息
    public void setProcessMsgWindow(Status status,string Msg,int ProcessValue)
    {
        Process_Cur_Status = status;
        ProcessMsgIconSwitch();
        Process_Cur_Task = Msg;
        Process_Cur_Value = ProcessValue;
    }
}
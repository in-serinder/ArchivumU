using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ArchivumU.ViewModels;

public class CloneViewModel:ViewModelBase
{
    // i18n 实例
    public I18nViewModel I18n => I18nViewModel.Instance;
    // 主窗口实例
    public MainWindowViewModel MainVm => MainWindowViewModel.Instance;
    
    public ObservableCollection<DeviceCloneInfo> CloneSourceInfoList { get; } = new ObservableCollection<DeviceCloneInfo>();
    public ObservableCollection<DeviceCloneInfo> CloneTargetInfoList { get; } = new ObservableCollection<DeviceCloneInfo>();

    public ObservableCollection<DeviceBaseInfo> TargetDevices { get; } = new ObservableCollection<DeviceBaseInfo>();
    public ObservableCollection<DeviceBaseInfo> SourceDevices { get; } = new ObservableCollection<DeviceBaseInfo>();
    public string LogText { get; set; } = "没有日志";
    
    public string ProgressText { get; set; } = "未开始";
    public int ProgressValue { get; set; } = 0;

    public CloneViewModel()
    {
        CloneSourceInfoList.Add(new DeviceCloneInfo("PassU", "16k", 1));
        CloneSourceInfoList.Add(new DeviceCloneInfo("Com1", "10", 1));
        CloneSourceInfoList.Add(new DeviceCloneInfo("Block", "60b", 100));
        CloneSourceInfoList.Add(new DeviceCloneInfo("Key&Value", "4k", 241));

        CloneTargetInfoList = CloneSourceInfoList;

        
        TargetDevices.Add(new DeviceBaseInfo("PassU", "COM1", "16k"));
        TargetDevices.Add(new DeviceBaseInfo("PASS2", "COM2", "10"));
        TargetDevices.Add(new DeviceBaseInfo("LinuxCode", "COM3", "60b"));
        TargetDevices.Add(new DeviceBaseInfo("BankPAss", "COM4", "4k"));
        
        SourceDevices =  TargetDevices;
        
    }
}



public class DeviceCloneInfo
{ 
    public string ItemName { get; set; } = string.Empty; //键或块
   public string SizeStr { get; set; }
   public long Count { get; set; }

   public DeviceCloneInfo(string itemName, string sizeStr, long count)
   {
       ItemName = itemName;
       SizeStr = sizeStr;
       Count = count;
       
   }
    
    
}


public class DeviceBaseInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public string SizeStr { get; set; } = string.Empty;


    public DeviceBaseInfo()
    {
        
    }
    public DeviceBaseInfo(string deviceName, string portName, string sizeStr)
    {
        DeviceName = deviceName;
        PortName = portName;
        SizeStr = sizeStr;
    }
}
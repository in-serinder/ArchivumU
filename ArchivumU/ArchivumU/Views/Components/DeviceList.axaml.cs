using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ArchivumU.ViewModels;
using ArchivumU.ViewModels;

namespace ArchivumU.Views.Components;

public partial class DeviceList : UserControl
{
    private feature_string feature_string = new feature_string();
    
    public ObservableCollection<ArchivumDevice> Devices { get; } = new ObservableCollection<ArchivumDevice>();
    public DeviceList()
    {
        InitializeComponent();
        
        Devices.Add(new ArchivumDevice("USB", "USB", "Connected", "USB1", 1000000000, 500000000));
        Devices.Add(new ArchivumDevice("SD", "SD", "Connected", "SD1", 1000000000, 500000000));
        Devices.Add(new ArchivumDevice("HDD", "HDD", "Connected", "HDD1", 1000000000000, 500000000000));
        LBDeviceList.ItemsSource = Devices;
    }
  }





public class ArchivumDevice
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
    public string PortName { get; set; }
    public long TotalSize { get; set; }
    public long UsedSize { get; set; }
    
    
    
    public string TotalSizeString => feature_string.formatSizeToString(TotalSize);
    public string UsedSizeString => feature_string.formatSizeToString(UsedSize);
    public string AvailableSizePercentString => feature_string.formatSizeToString(UsedSize / TotalSize*100)+"%";
    
    public ArchivumDevice()
    {
        
    }
    
    
    public ArchivumDevice(string name, string type, string status, string portName, long totalSize, long usedSize)
    {
        Name = name;
        Type = type;
        Status = status;
        PortName = portName;
        TotalSize = totalSize;
        UsedSize = usedSize;
    }
}

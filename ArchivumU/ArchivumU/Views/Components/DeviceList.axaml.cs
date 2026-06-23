using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ArchivumU.ViewModels;
using ArchivumU.ViewModels;
using Avalonia.VisualTree;

namespace ArchivumU.Views.Components;

public partial class DeviceList : UserControl
{
    private feature_string feature_string = new feature_string();
    
    public ObservableCollection<ArchivumDevice> Devices { get; } = new ObservableCollection<ArchivumDevice>();
    // 设备选中事件 - 供外部订阅
    public event EventHandler<ArchivumDevice>? DeviceSelected;
    
    public DeviceList()
    {
        InitializeComponent();
        
        Devices.Add(new ArchivumDevice("USB", "USB", "Connected", "USB1", 1000000000, 500000000));
        Devices.Add(new ArchivumDevice("SD", "SD", "Connected", "SD1", 1000000000, 500000000));
        Devices.Add(new ArchivumDevice("HDD", "HDD", "Connected", "HDD1", 1000000000000, 500000000000));
        LBDeviceList.ItemsSource = Devices;
    }

    private void showAuthWindow(string device, string portName)
    {
        Window? ownerWindow = this.FindAncestorOfType<Window>();
        if (ownerWindow is null)
            return;

        var mainVm = DataContext as MainWindowViewModel;
        var authWin = new AuthWindow(mainVm, device, portName);
        authWin.ShowDialog(ownerWindow);
    }
    
    // 设备选中事件处理
    private void OnDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (LBDeviceList.SelectedItem is ArchivumDevice selectedDevice)
        {
            // 执行选中逻辑
            HandleDeviceSelection(selectedDevice);
            
            // 触发外部事件通知
            DeviceSelected?.Invoke(this, selectedDevice);
            
            showAuthWindow(selectedDevice.Name, selectedDevice.PortName);
        }
    }
    
    // 处理设备选中逻辑
    private void HandleDeviceSelection(ArchivumDevice device)
    {
        // 这里可以添加设备选中后的业务逻辑
        // 例如：加载设备数据、更新UI状态等
        System.Diagnostics.Debug.WriteLine($"Selected Device: {device.Name} ({device.Type})");
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
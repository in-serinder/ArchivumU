using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArchivumU.Services;
using ArchivumU.Views.Components;

namespace ArchivumU.ViewModels;

public class DeviceListViewModel : ViewModelBase
{
    // 全局唯一静态实例
    public static DeviceListViewModel Instance { get; } = new DeviceListViewModel();

    public I18nViewModel I18n => I18nViewModel.Instance;

    // 共享设备列表，全局唯一
    public ObservableCollection<ArchivumDevice> DeviceList { get; set; } = new ObservableCollection<ArchivumDevice>();

    // 私有构造：禁止外部new，保证全局只有一个对象
    private DeviceListViewModel()
    {
    }

    // 刷新设备列表
    public void RefuseDevice()
    {
        DeviceList.Clear();
        List<DeviceConfig> deviceConfigs = ConfigJsonService.Instance.GetAllDevices();
        foreach (var device in deviceConfigs)
        {
            var encryptType = FormatEncryptionType(device.EncryptionMode);
            DeviceList.Add(new ArchivumDevice(
                device.Name,
                "UART",
                "Disconnected",
                device.Port,
                device.Storage.TotalSize,
                device.Storage.UsedSize,
                encryptType));
        }
    }

    // 字符串转加密枚举
    public DevInitViewModelItem.EncryptionType FormatEncryptionType(string type)
    {
        switch (type)
        {
            case "NON":
                return DevInitViewModelItem.EncryptionType.NoneEncryption;
            case "AES128": // 修正你之前存的是AES128，不是AES
                return DevInitViewModelItem.EncryptionType.AES128;
            case "XOR":
                return DevInitViewModelItem.EncryptionType.XOR;
            case "CESAR":
                return DevInitViewModelItem.EncryptionType.Caesar;
            case "RC4":
                return DevInitViewModelItem.EncryptionType.RC4;
            default:
                return DevInitViewModelItem.EncryptionType.NoneEncryption;
        }
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

    public DevInitViewModelItem.EncryptionType EncryptionType { get; set; } =
        DevInitViewModelItem.EncryptionType.NoneEncryption;

    public string TotalSizeString => feature_string.formatSizeToString(TotalSize);

    public string UsedSizeString => feature_string.formatSizeToString(UsedSize);

    public string AvailableSizePercentString => string.Format("{0:0.00}%",
        (UsedSize<=0?0:(UsedSize / TotalSize * 100)));

    public ArchivumDevice()

    {
    }

    public ArchivumDevice(string name, string type, string status, string portName, long totalSize, long usedSize,
        DevInitViewModelItem.EncryptionType encryptionType = DevInitViewModelItem.EncryptionType.NoneEncryption)

    {
        Name = name;

        Type = type;

        Status = status;

        PortName = portName;

        TotalSize = totalSize;

        UsedSize = usedSize;

        EncryptionType = encryptionType;
    }
}
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    /// <summary>当前处于「已连接」状态的设备（双击连接成功后赋值）。</summary>
    public ArchivumDevice? ConnectedDevice { get; private set; }

    // 私有构造：禁止外部new，保证全局只有一个对象
    private DeviceListViewModel()
    {
    }

    // 刷新设备列表
    public void RefuseDevice()
    {
        // 记录刷新前仍处于连接状态的端口，重建后恢复其状态
        var connectedPorts = new HashSet<string>();
        foreach (var d in DeviceList)
        {
            if (d.IsConnected)
            {
                connectedPorts.Add(d.PortName);
            }
        }

        DeviceList.Clear();
        List<DeviceConfig> deviceConfigs = ConfigJsonService.Instance.GetAllDevices();
        foreach (var device in deviceConfigs)
        {
            bool isConn = connectedPorts.Contains(device.Port);
            var encryptType = FormatEncryptionType(device.EncryptionMode);
            var item = new ArchivumDevice(
                device.Name,
                "UART",
                isConn ? I18n.Connected : I18n.Disconnected,
                device.Port,
                device.Storage.TotalSize,
                device.Storage.UsedSize,
                encryptType)
            {
                IsConnected = isConn
            };
            DeviceList.Add(item);

            // 同步维护「当前连接设备」引用
            if (isConn && (ConnectedDevice == null || ConnectedDevice.PortName == device.Port))
            {
                ConnectedDevice = item;
            }
        }
    }

    /// <summary>
    /// 依据端口号设置设备的连接状态（一切以端口号为主键）。
    /// </summary>
    public void SetConnected(string portName, bool connected)
    {
        foreach (var device in DeviceList)
        {
            if (device.PortName == portName)
            {
                device.IsConnected = connected;
                device.Status = connected ? I18n.Connected : I18n.Disconnected;
                ConnectedDevice = connected ? device : (ConnectedDevice == device ? null : ConnectedDevice);
                break;
            }
        }
    }

    /// <summary>
    /// 依据端口号更新设备列表项的存储容量（总容量 / 已用容量）。
    /// 用于把上位机按块与键值对计算出的「已用空间」同步回列表显示。
    /// </summary>
    public void UpdateStorage(string portName, long totalSize, long usedSize)
    {
        foreach (var device in DeviceList)
        {
            if (device.PortName == portName)
            {
                if (totalSize > 0)
                {
                    device.TotalSize = totalSize;
                }
                device.UsedSize = usedSize;
                break;
            }
        }
    }

    // 字符串转加密枚举
    public DevInitViewModelItem.EncryptionType FormatEncryptionType(string type)
    {
        switch (type)
        {
            case "NON":
                return DevInitViewModelItem.EncryptionType.NoneEncryption;
            case "AES128":
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

public class ArchivumDevice : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _type = string.Empty;
    private string _status = string.Empty;
    private string _portName = string.Empty;
    private long _totalSize;
    private long _usedSize;
    private bool _isConnected;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Type
    {
        get => _type;
        set => SetField(ref _type, value);
    }

    /// <summary>连接状态文本（本地化，如「已连接 / 未连接」）。</summary>
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string PortName
    {
        get => _portName;
        set => SetField(ref _portName, value);
    }

    public long TotalSize
    {
        get => _totalSize;
        set
        {
            if (SetField(ref _totalSize, value))
            {
                OnPropertyChanged(nameof(TotalSizeString));
                OnPropertyChanged(nameof(AvailableSizePercentString));
            }
        }
    }

    public long UsedSize
    {
        get => _usedSize;
        set
        {
            if (SetField(ref _usedSize, value))
            {
                OnPropertyChanged(nameof(UsedSizeString));
                OnPropertyChanged(nameof(AvailableSizePercentString));
            }
        }
    }

    /// <summary>是否已连接（双击连接成功置为 true）。</summary>
    public bool IsConnected
    {
        get => _isConnected;
        set => SetField(ref _isConnected, value);
    }

    public DevInitViewModelItem.EncryptionType EncryptionType { get; set; } =
        DevInitViewModelItem.EncryptionType.NoneEncryption;

    public string TotalSizeString => feature_string.formatSizeToString(TotalSize);

    public string UsedSizeString => feature_string.formatSizeToString(UsedSize);

    //public string AvailableSizePercentString => string.Format("{0:0.00}%",
    //    (UsedSize <= 0 ? 0 : (UsedSize / TotalSize * 100)));

    //public string AvailableSizePercentString => "20%";

    public string AvailableSizePercentString => string.Format("{0:0.00}%",
    (UsedSize <= 0 ? 0 : (UsedSize / TotalSize * 100)));

    public ArchivumDevice()
    {
    }

    public ArchivumDevice(string name, string type, string status, string portName, long totalSize, long usedSize,
        DevInitViewModelItem.EncryptionType encryptionType = DevInitViewModelItem.EncryptionType.NoneEncryption)
    {
        _name = name;
        _type = type;
        _status = status;
        _portName = portName;
        _totalSize = totalSize;
        _usedSize = usedSize;
        EncryptionType = encryptionType;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

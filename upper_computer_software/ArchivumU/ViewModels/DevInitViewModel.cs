using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ArchivumU.Models;
using ArchivumU.Views.Components;

namespace ArchivumU.ViewModels;

public class DevInitViewModel : ViewModelBase, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // i18n 实例
    public I18nViewModel I18n => I18nViewModel.Instance;
    public SerialObjectHelperModel SerialObjectHelperModel = new SerialObjectHelperModel();

    private DevInitViewModelItem _item;
    public DevInitViewModelItem Item
    {
        get => _item;
        set
        {
            _item = value;
            OnPropertyChanged();
        }
    }

    private string _title = $"INIT:Com1";
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public DevInitViewModel()
    {
        Item = new DevInitViewModelItem("NoCom1", "Unknown");
        Title = $"{I18n.InitDev}:{Item.Portname}";
    }

    public DevInitViewModel(string Portname)
    {
        Item = new DevInitViewModelItem(Portname, "GetVersion ...");
        List<string> portlist = SerialObjectHelperModel.GetFreePortNames();
        // 修复bug：集合数量不可能小于0，判断空集合用 Count == 0
        if (portlist.Count == 0)
        {
            Item.PortnameList = portlist;
            Item.Portname = "NoFreePort";
        }
        else
        {
            Item.PortnameList = portlist;
            Item.Portname = portlist[0];
        }
        Title = $"{I18n.InitDev}:{Item.Portname}";
    }
}

public class DevInitViewModelItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum EncryptionType
    {
        NoneEncryption,
        AES128,
        XOR,
        Caesar,
        RC4
    }

    private string _portname = "Unknow";
    public string Portname
    {
        get => _portname;
        set
        {
            _portname = value;
            OnPropertyChanged();
        }
    }

    private List<string> _portnameList = new List<string>();
    public List<string> PortnameList
    {
        get => _portnameList;
        set
        {
            _portnameList = value;
            OnPropertyChanged();
        }
    }

    private string _firmWareVersion = "Unknown";
    public string FirmWareVersion
    {
        get => _firmWareVersion;
        set
        {
            _firmWareVersion = value;
            OnPropertyChanged();
        }
    }

    private string _devName = "ArchivumU";
    public string DevName
    {
        get => _devName;
        set
        {
            _devName = value;
            OnPropertyChanged();
        }
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged();
        }
    }

    private int _keyNumberInBlock = 16;
    public int KeyNumberInBlock
    {
        get => _keyNumberInBlock;
        set
        {
            _keyNumberInBlock = value;
            OnPropertyChanged();
        }
    }

    private int _blockLengthLim = 20;
    public int BlockLengthLim
    {
        get => _blockLengthLim;
        set
        {
            _blockLengthLim = value;
            OnPropertyChanged();
        }
    }

    private bool _enablePassword = false;
    public bool EnablePassword
    {
        get => _enablePassword;
        set
        {
            _enablePassword = value;
            OnPropertyChanged();
        }
    }

    private EncryptionType _encryptionAlgorithm = EncryptionType.NoneEncryption;
    public EncryptionType EncryptionAlgorithm
    {
        get => _encryptionAlgorithm;
        set
        {
            _encryptionAlgorithm = value;
            OnPropertyChanged();
        }
    }

    public List<EncryptionType> EncryptionAlgoritems { get; set; } = new List<EncryptionType>((EncryptionType[])Enum.GetValues(typeof(EncryptionType)));

    public DevInitViewModelItem()
    {

    }

    public DevInitViewModelItem(string portname, string firmwareversion, EncryptionType encryptionType = EncryptionType.NoneEncryption)
    {
        Portname = portname;
        FirmWareVersion = firmwareversion;
        EncryptionAlgorithm = encryptionType;
    }
}
using System;
using System.Collections.Generic;
using ArchivumU.Views.Components;

namespace ArchivumU.ViewModels;

public class DevInitViewModel : ViewModelBase
{
    // i18n 实例
    public I18nViewModel I18n => I18nViewModel.Instance;
    
    public DevInitViewModelItem Item {get;set;} = new DevInitViewModelItem();
    // 窗口标题
    public string Title {get;set;} = $"INIT:Com1";
    
    public DevInitViewModel()
    {
        Item = new DevInitViewModelItem("Com1", "1.0.0");

        Title = $"{I18n.InitDev}:{Item.Portname}";
    }
}


public class DevInitViewModelItem
{
    
    public enum EncryptionType
    {
        NoneEncryption,
        AES128,
        XOR,
        Caesar,
        RC4
    }
    public string Portname {get;set;} = "Unknow";
    public string FirmWareVersion {get;set;} = "Unknown";
    public string DevName {get;set;} = "ArchivumU";
    public string Password {get;set;} = "";
    public int KeyNumberInBlock {get;set;} = 16; //可更改
    public int  BlockLengthLim {get;set;} = 20; //块名最长20个字符
    public bool EnablePassword {get;set;} = false;
    // public bool EnableEncryption {get;set;} = false;
    public EncryptionType EncryptionAlgorithm {get;set;} = EncryptionType.NoneEncryption;
    
    public List<EncryptionType> EncryptionAlgoritems {get;set;} = new List<EncryptionType>((EncryptionType[])Enum.GetValues(typeof(EncryptionType)));
    
    public DevInitViewModelItem()
    {
        
    }
    
    public  DevInitViewModelItem(string portname, string firmwareversion,EncryptionType encryptionType=EncryptionType.NoneEncryption)
    {
        Portname = portname;
        FirmWareVersion = firmwareversion;
        EncryptionAlgorithm = encryptionType;
    }
}
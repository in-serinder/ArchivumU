using System;
using System.Collections.Generic;
using System.Diagnostics;
using ArchivumU.Models;
using ArchivumU.Services;
using ArchivumU.ViewModels;
using ArchivumU.Views.Components;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArchivumU.Views;

public partial class DevInitWindow : Window
{
    // 强类型快捷获取VM
    public DevInitViewModel? Vm => DataContext as DevInitViewModel;
    private DevBaseInfo DevBaseInfo = null;
    public bool isNonInit = false;

    public DevInitWindow()
    {
        InitializeComponent();
        // 无参构造默认创建空VM
        DataContext = new DevInitViewModel();
    }
    


    public DevInitWindow(string Portname)
    {
        InitializeComponent();
        // 正确：实例化ViewModel并赋值给DataContext
        DataContext = new DevInitViewModel(Portname);

        // 判空防止异常
        if (Vm != null)
        {
            Vm.Item.Portname = Portname;
            Vm.Title = $"{Vm.I18n.InitDev}:{Vm.Item.Portname}";
        }


        //初始化进入时第一个检查
        CBINITCOMBO.SelectedIndex = 0;
        CBINITCOMBO_OnDropDownClosed(null, null);
    }

    private void BTNDICancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void CBINITCOMBO_OnDropDownClosed(object? sender, EventArgs e)
    {   
        //预执行 解决缓冲区滞后问题
        await Vm.SerialObjectHelperModel.QuickSendCommand(Vm.Item.Portname, AT_CMDTXHelperModel.AT_INFO());        Vm.Title = $"{Vm.I18n.InitDev}:{Vm.Item.Portname}";
        var (ret, dev_check) =
            await Vm.SerialObjectHelperModel.QuickSendCommand(Vm.Item.Portname, AT_CMDTXHelperModel.AT_INFO());
        DevBaseInfo devinfo = AT_CMDRXPeaserHelperModel.ParseInfoResponse(dev_check);

        DevBaseInfo = devinfo;

        if (ret && devinfo != null)
        {
            Vm.Item.FirmWareVersion = devinfo.FirmwareVersion;

            // 可以判断设备是否已初始化
            if (devinfo.DeviceName != null)
            {
                // 已初始化设备 把所有设置禁用
                TBINITNAME.IsEnabled = false;
                CBENABLEPASSWORD.IsEnabled = false;
                CBINITENCRYPTALGO.IsEnabled = false;
                // 配置获取已初始化设备的配置
                Vm.Item.DevName = devinfo.DeviceName;
                Vm.Item.EnablePassword = devinfo.PasswordStatus;
                Vm.Item.Password = "Unknown";
                Vm.Item.EncryptionAlgorithm = DeviceListViewModel.Instance.FormatEncryptionType(devinfo.EncryptionType);
                Debug.WriteLine($"设备名: {devinfo.DeviceName}");
                Vm.Item.FirmWareVersion = $"{devinfo.DeviceName}@{devinfo.FirmwareVersion} (AlreadyInitialized)";
                isNonInit = false;
            }
            else
            {
                TBINITNAME.IsEnabled = true;
                CBENABLEPASSWORD.IsEnabled = true;
                CBINITENCRYPTALGO.IsEnabled = true;
                // 未初始化设备
                Debug.WriteLine("设备未初始化");
                isNonInit = true;
            }
        }
        else
        {
            Vm.Item.FirmWareVersion = "Err Not ArchivumU Device OR Port Used";
        }
    }

    public string EncryptTypeToString(DevInitViewModelItem.EncryptionType type)
    {
        return type switch
        {
            DevInitViewModelItem.EncryptionType.NoneEncryption => "NON",
            DevInitViewModelItem.EncryptionType.AES128 => "AES128",
            DevInitViewModelItem.EncryptionType.XOR => "XOR",
            DevInitViewModelItem.EncryptionType.Caesar => "CAESAR",
            DevInitViewModelItem.EncryptionType.RC4 => "RC4",
            _ => "NON"
        };
    }

    private async void BTNDIInit_OnClick(object? sender, RoutedEventArgs e)
    {
        string init_result = "";
        (bool ret_newinfo, string dev_check) = await Vm.SerialObjectHelperModel.QuickSendCommand(Vm.Item.Portname, AT_CMDTXHelperModel.AT_INFO());
        DevBaseInfo devinfo_new = AT_CMDRXPeaserHelperModel.ParseInfoResponse(dev_check);
        //初始化前检查是否是ArchivumU设备且未初始化
        if (isNonInit)
        {
            //初始化流程
            var password = Vm.Item.EnablePassword ? Vm.Item.Password : "";
            (bool ret, string result) = await Vm.SerialObjectHelperModel.QuickSendCommand(Vm.Item.Portname,
                AT_CMDTXHelperModel.AT_INIT(Vm.Item.DevName, Vm.Item.EnablePassword, password,
                    Vm.Item.EncryptionAlgorithm));
            Debug.WriteLine(result);

            if (ret && result.Contains("DATA+OK"))
            {
                Vm.Item.FirmWareVersion = "Init Success";
                isNonInit = false;

                

                var storageInfo = new StorageInfo()
                {
                    TotalSize = DevBaseInfo.TotalSize,
                    UsedSize = 0
                };
                ConfigJsonService.Instance.AddDevice(new DeviceConfig()
                {
                    Name = DevBaseInfo.DeviceName,
                    Port = Vm.Item.Portname,
                    EncryptionMode = EncryptTypeToString(Vm.Item.EncryptionAlgorithm),
                    Storage = storageInfo
                });
                Close();
                return;
            }
  
            
            //当设备为格式化设备时候
            //报错时呼出错误窗口
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error, "Init Failed", $"Init Result: {result} \n  Please Check {Vm.Item.Portname} Is Used By Other Process");
            return;
        }
        //已初始化设备进行直接添加
        if(ret_newinfo && devinfo_new != null)
        {
            Debug.WriteLine($"添加已初始化设备: {devinfo_new.DeviceName} {devinfo_new.FirmwareVersion} {devinfo_new.TotalSize} {devinfo_new.EncryptionType} {devinfo_new.AccessCount} {devinfo_new.PasswordStatus}");
            var storageInfo = new StorageInfo()
            {
                TotalSize = devinfo_new.TotalSize,
                UsedSize = 0
            };
            ConfigJsonService.Instance.AddDevice(new DeviceConfig()
            {
                Name = devinfo_new.DeviceName,
                Port = Vm.Item.Portname,
                EncryptionMode = devinfo_new.EncryptionType,
                Storage = storageInfo
            });
            Close();
            return;
        }
        InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error, "Init Failed", $"Device {Vm.Item.Portname} Already Init And Join DeviceList");
        
        DeviceListViewModel.Instance.RefuseDevice();
        Close();
    }

    private void CBENABLEPASSWORD_OnClick(object? sender, RoutedEventArgs e)
    {
        if (CBENABLEPASSWORD.IsChecked == true)
        {
            Vm.Item.EnablePassword = true;
        }
        else
        {
            Vm.Item.EnablePassword = false;
        }
    }
}
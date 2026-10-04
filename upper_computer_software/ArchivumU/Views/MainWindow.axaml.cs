using System;
using System.Diagnostics;
using System.Globalization;
using ArchivumU.I18n;
using ArchivumU.Models;
using ArchivumU.ViewModels;
using ArchivumU.Views.Components;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace ArchivumU.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 订阅文化变化事件
            DataContext = new MainWindowViewModel();
            // 初始化
            DialogMgrModel.Initialize(this);
            // private SerialObjectHelperModel serialObjectHelperModel = new SerialObjectHelperModel();

            string key = "1234567890123456";
            string iv = "1234567890123456";

            // 调试用
            // new DeviceClone().Show();
            // string test = AES128EncHelperModel.Aes128Encrypt("Test", key, iv);
            // new DevInitWindow().Show();
            // Debug.WriteLine($"AES128EncModel ENC: {test}");
            // Debug.WriteLine($"AES128EncModel DEC: {AES128EncHelperModel.Aes128Decrypt(test, key, iv)}");
            // Debug.WriteLine($"Random Key: {AES128EncHelperModel.Generate16KeyOrIv()}");
            // string test = new XOREncHelperModel().XorEncryptDecrypt("Test", key);
            // Debug.WriteLine($"XOREncHelperModel ENC: {test}");
            // Debug.WriteLine($"XOREncHelperModel DEC: {new XOREncHelperModel().XorDecodeFromBase64(test, key)}");
            // string test = new CaesarEncHelperModel().Encrypt("Test", new CaesarEncHelperModel().GetRecoverShiftByCipherOnly(key));
            // Debug.WriteLine($"CaesarEncHelperModel ENC: {test}");
            // Debug.WriteLine($"CaesarEncHelperModel DEC: {new CaesarEncHelperModel().Decrypt(test, new CaesarEncHelperModel().GetRecoverShiftByCipherOnly(key))}");
            string test = new RC4EncHelperModel().Encrypt("Test", key);
            Debug.WriteLine($"RC4EncHelperModel ENC: {test}");
            Debug.WriteLine($"RC4EncHelperModel DEC: {new RC4EncHelperModel().Decrypt(test, key)}");

            // new ProcessDialogWindow().Show();
            // new ProcessDialogWindow().Show();
            // InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Success, "Success", "Test");
            // ProcessMsgViewModel.ShowProcessWindow(ProcessMsgViewModel.Status.Processing, "测试任务", 20);




        }


        private void MIDevClone_OnClick(object? sender, RoutedEventArgs e)
        {
            new DeviceCloneWindow().Show();
        }

        private async void MIADDDEV_OnClick(object? sender, RoutedEventArgs e)
        {
            var helper = new SerialObjectHelperModel();
            var portList = helper.GetFreePortNames();
            if (portList.Count <= 0)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error,
                    I18nViewModel.Instance.Err, "No available serial ports found.");
                return;
            }

            // 使用第一个空闲端口进入初始化/添加流程
            await DialogMgrModel.ShowDialogAsync(new DevInitWindow(portList[0]));

            // 完成后刷新设备列表（以端口号为主键同步）
            DeviceListViewModel.Instance.RefuseDevice();
        }

        private void MIRefresh_OnClick(object? sender, RoutedEventArgs e)
        {
            DeviceListViewModel.Instance.RefuseDevice();
        }

        #region 设备菜单

        // 移除设备（断开并清空当前连接）
        private void MIRemoveDev_OnClick(object? sender, RoutedEventArgs e)
        {
            var device = DeviceListViewModel.Instance.ConnectedDevice;
            if (device == null)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                    I18nViewModel.Instance.Warn, I18nViewModel.Instance.PleaseSelectBlock);
                return;
            }

            DeviceListViewModel.Instance.SetConnected(device.PortName, false);
            DeviceDataViewModel.Instance.Clear();
        }

        // 重命名设备（修改上位机配置里的设备名）
        private async void MIRenameDev_OnClick(object? sender, RoutedEventArgs e)
        {
            var device = DeviceListViewModel.Instance.ConnectedDevice;
            if (device == null)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                    I18nViewModel.Instance.Warn, I18nViewModel.Instance.PleaseSelectBlock);
                return;
            }

            var result = await DialogMgrModel.ShowDialogAsync<BlockAddResult>(
                new BlockAddWindow(new BlockAddViewModel { Title = I18nViewModel.Instance.Rename, BlockName = device.Name }));

            if (result == null)
            {
                return;
            }

            var service = Services.ConfigJsonService.Instance;
            var cfg = service.GetDeviceByPort(device.PortName);
            if (cfg != null)
            {
                string oldName = cfg.Name;
                cfg.Name = result.BlockName;
                service.UpdateDevice(oldName, cfg);
                DeviceListViewModel.Instance.RefuseDevice();
            }
        }

        // 备份设备到本地（占位：提示待实现）
        private void MIDevBackup_OnClick(object? sender, RoutedEventArgs e)
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.BackupToLocal, "TODO");
        }

        // 格式化整个设备
        private async void MIDevFormat_OnClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort))
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                    I18nViewModel.Instance.Warn, I18nViewModel.Instance.NotArchivumUDevice);
                return;
            }

            await DeviceDataViewModel.Instance.FormatDeviceAsync();
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Success,
                I18nViewModel.Instance.DeviceFormat, DeviceDataViewModel.Instance.CurrentPort);
        }

        #endregion

        #region 块菜单

        private void MIBlockRefresh_OnClick(object? sender, RoutedEventArgs e)
        {
            BLKList?.RefreshBlocks();
        }

        private void MIBlockCreate_OnClick(object? sender, RoutedEventArgs e)
        {
            BLKList?.AddBlockDialog();
        }

        private void MIBlockRename_OnClick(object? sender, RoutedEventArgs e)
        {
            BLKList?.RenameSelectedBlock();
        }

        private void MIBlockDelete_OnClick(object? sender, RoutedEventArgs e)
        {
            BLKList?.DeleteSelectedBlock();
        }

        private void MIBlockBackup_OnClick(object? sender, RoutedEventArgs e)
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.BackupToLocal, "TODO");
        }

        private void MIBlockLformat_OnClick(object? sender, RoutedEventArgs e)
        {
            BLKList?.FormatSelectedBlock();
        }

        #endregion

        #region 键值菜单

        // 创建键值对：需先选中块
        private async void MIKeyCreate_OnClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort))
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                    I18nViewModel.Instance.Warn, I18nViewModel.Instance.NotArchivumUDevice);
                return;
            }

            if (DeviceDataViewModel.Instance.SelectedBlockId < 0)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                    I18nViewModel.Instance.Warn, I18nViewModel.Instance.PleaseSelectBlock);
                return;
            }

            var result = await DialogMgrModel.ShowDialogAsync<KeyAddResult>(
                new KeyAddWindow(new KeyAddViewModel()));

            if (result == null)
            {
                return;
            }

            bool ok = await DeviceDataViewModel.Instance.CreateKeyAsync(
                DeviceDataViewModel.Instance.SelectedBlockId, result.Key, result.Value);

            InfoDialogViewModel.Show(
                ok ? InfoDialogViewModel.InfoType.Success : InfoDialogViewModel.InfoType.Error,
                ok ? I18nViewModel.Instance.KeyAddSuccess : I18nViewModel.Instance.KeyAddFailed,
                result.Key);
        }

        // 删除键：走 KeyValueMgr 的选中项
        private void MIKeyRemove_OnClick(object? sender, RoutedEventArgs e)
        {
            KVMgr?.DeleteSelectedKey();
        }

        #endregion
    }
}
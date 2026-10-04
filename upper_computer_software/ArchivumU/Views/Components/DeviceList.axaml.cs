using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArchivumU.Models;
using ArchivumU.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ArchivumU.ViewModels;
using Avalonia.VisualTree;

namespace ArchivumU.Views.Components;

public partial class DeviceList : UserControl
{
    private feature_string feature_string = new feature_string();
    // 设备选中事件 - 供外部订阅
    public event EventHandler<ArchivumDevice>? DeviceSelected;

    /// <summary>供 XAML（如右键菜单）绑定使用的本地化实例。</summary>
    public I18nViewModel I18n => I18nViewModel.Instance;

    public DeviceList()
    {
        InitializeComponent();
        DeviceListViewModel.Instance.RefuseDevice();
        if (DeviceListViewModel.Instance.DeviceList.Count > 0)
        {
            DevListEmptyPlaceholder.IsVisible = false;
        }
        else
        {
            DevListEmptyPlaceholder.IsVisible = true;
        }
        DataContext = this;
        LBDeviceList.ItemsSource = DeviceListViewModel.Instance.DeviceList;
    }

    // 单击：仅选中（记录选中设备），不触发连接
    private void OnDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (LBDeviceList.SelectedItem is ArchivumDevice selectedDevice)
        {
            HandleDeviceSelection(selectedDevice);
            DeviceSelected?.Invoke(this, selectedDevice);
        }
    }

    // 右键菜单：断开设备
    private void MIDisconnectDevice_OnClick(object? sender, RoutedEventArgs e)
    {
        // ContextMenu 的 DataContext 指向被右键的那一项
        ArchivumDevice? device = (sender as MenuItem)?.DataContext as ArchivumDevice
                                 ?? LBDeviceList.SelectedItem as ArchivumDevice;
        if (device == null)
        {
            return;
        }

        DisconnectDevice(device);
    }

    /// <summary>断开指定设备：复位连接状态并清空数据视图。</summary>
    private void DisconnectDevice(ArchivumDevice device)
    {
        // 仅断开当前已连接的设备
        DeviceListViewModel.Instance.SetConnected(device.PortName, false);

        // 若断开的正是当前数据端口，则清空块/键值对
        if (DeviceDataViewModel.Instance.CurrentPort == device.PortName)
        {
            DeviceDataViewModel.Instance.Clear();
        }

        InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Success,
            I18nViewModel.Instance.DeviceDisconnected,
            $"{device.Name}@{device.PortName}");
    }

    // 双击：执行设备连接业务流
    private async void LBDeviceList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LBDeviceList.SelectedItem is ArchivumDevice selectedDevice)
        {
            await ConnectDeviceAsync(selectedDevice);
        }
    }

    // 处理设备选中逻辑（仅本地 UI 状态）
    private void HandleDeviceSelection(ArchivumDevice device)
    {
        System.Diagnostics.Debug.WriteLine($"Selected Device: {device.Name} ({device.Type})");
    }

    /// <summary>
    /// 双击连接设备的完整业务流：
    ///   1) 读取设备 INFO，校验是否为 ArchivumU 设备（以端口号为主键）。
    ///   2) 下位机信息覆盖上位机已有信息并同步配置。
    ///   3) 未初始化 -&gt; 弹出初始化窗口；需密码 -&gt; 弹出认证窗口；否则直连。
    ///   4) 连接成功后读取块列表与键值对。
    /// </summary>
    private async System.Threading.Tasks.Task ConnectDeviceAsync(ArchivumDevice device)
    {
        // 1) 探测设备
        var probe = await DeviceConnectionService.ProbeAsync(device.PortName);

        switch (probe.State)
        {
            case DeviceConnectionService.ConnectionState.NotArchivumU:
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error,
                    I18nViewModel.Instance.Err,
                    I18nViewModel.Instance.NotArchivumUDevice);
                DeviceListViewModel.Instance.SetConnected(device.PortName, false);
                return;

            case DeviceConnectionService.ConnectionState.NeedInit:
                {
                    // 2) 未初始化：弹出初始化窗口（内部完成初始化/添加设备）
                    await DialogMgrModel.ShowDialogAsync(new DevInitWindow(device.PortName));

                    // 初始化完成后刷新设备列表并重新探测，尝试直连
                    DeviceListViewModel.Instance.RefuseDevice();
                    var again = await DeviceConnectionService.ProbeAsync(device.PortName);
                    if (again.State == DeviceConnectionService.ConnectionState.Ready
                        || again.State == DeviceConnectionService.ConnectionState.NeedAuth)
                    {
                        await ContinueConnectAsync(device, again);
                    }
                    return;
                }

            case DeviceConnectionService.ConnectionState.NeedAuth:
                await ContinueConnectAsync(device, probe);
                return;

            case DeviceConnectionService.ConnectionState.Ready:
                await ContinueConnectAsync(device, probe);
                return;
        }
    }

    /// <summary>处理「需要认证」与「直接连接」的分支。</summary>
    private async System.Threading.Tasks.Task ContinueConnectAsync(
        ArchivumDevice device,
        DeviceConnectionService.ConnectionProbe probe)
    {
        // 需密码：弹出认证窗口
        if (probe.State == DeviceConnectionService.ConnectionState.NeedAuth)
        {
            bool authed = await DialogMgrModel.ShowDialogAsync<bool>(
                new AuthWindow(device.Name, device.PortName));

            if (!authed)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error,
                    I18nViewModel.Instance.AuthenticationFailed,
                    I18nViewModel.Instance.AuthenticationFailed);
                DeviceListViewModel.Instance.SetConnected(device.PortName, false);
                return;
            }
        }

        // 2) 同步配置：下位机信息覆盖上位机已有信息（按端口号主键）
        if (probe.Info != null)
        {
            DeviceConnectionService.SyncDeviceConfig(probe.Info, device.PortName);

            // 更新列表中该设备显示的信息
            device.Name = string.IsNullOrEmpty(probe.Info.DeviceName) ? device.Name : probe.Info.DeviceName;
            device.TotalSize = probe.Info.TotalSize;
            device.EncryptionType = DeviceListViewModel.Instance.FormatEncryptionType(probe.Info.EncryptionType);
        }

        // 3) 标记连接状态
        DeviceListViewModel.Instance.SetConnected(device.PortName, true);

        // 4) 联动块列表与键值对：设置当前端口并读取块
        //    （刷新块的同时会刷新存储容量，并自动同步到设备列表与配置）
        DeviceDataViewModel.Instance.SetPort(device.PortName);
        await DeviceDataViewModel.Instance.RefreshBlocksAsync();

        InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Success,
            I18nViewModel.Instance.DeviceConnected,
            $"{device.Name}@{device.PortName}");
    }
}

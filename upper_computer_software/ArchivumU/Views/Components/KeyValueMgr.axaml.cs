using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ArchivumU.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArchivumU.Views.Components;

public partial class KeyValueMgr : UserControl
{
    // 当前操作块 ID（取自共享的「当前设备数据」视图模型）
    private int CurrentBlockId => DeviceDataViewModel.Instance.SelectedBlockId;

    public KeyValueMgr()
    {
        InitializeComponent();
        // 绑定到全局共享的「当前设备数据」视图模型
        DataContext = DeviceDataViewModel.Instance;
        if (EmptyPlaceholderINKVMGR != null)
        {
            EmptyPlaceholderINKVMGR.IsVisible = false;
        }
    }

    // 选中键值对变化：记录到共享视图模型，供菜单「删除键」使用
    private void LBKeyValueList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        DeviceDataViewModel.Instance.SelectedKeyValue = LBKeyValueList.SelectedItem as KeyValueItem;
    }

    /// <summary>删除当前选中的键值对（供菜单「删除键」调用）。</summary>
    public async void DeleteSelectedKey()
    {
        var item = LBKeyValueList.SelectedItem as KeyValueItem;
        if (item == null)
        {
            InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Warning,
                I18nViewModel.Instance.Warn, I18nViewModel.Instance.KeyRemove);
            return;
        }

        if (!string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort)
            && DeviceDataViewModel.Instance.SelectedBlockId >= 0)
        {
            bool ok = await DeviceDataViewModel.Instance.DeleteKeyAsync(
                DeviceDataViewModel.Instance.SelectedBlockId, item.Key);
            if (!ok)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error,
                    I18nViewModel.Instance.KeyDelFailed, item.Key);
            }
        }
        else
        {
            DeviceDataViewModel.Instance.KeyValueItems.Remove(item);
        }
    }
    // 复制按钮点击事件：仅把该键值对的 value 复制到剪贴板
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var item = button?.DataContext as KeyValueItem;
        if (item == null)
        {
            return;
        }

        string text = item.Value ?? string.Empty;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);

        // 复制成功提示（仅提示内容本身，不含 key）
        //InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Success,
        //    I18nViewModel.Instance.CopyToClipboard, text);
    }

    // 编辑按钮点击事件
    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var item = button?.DataContext as KeyValueItem;
        if (item != null)
        {
            // 使用 Parent 属性链查找
            var parent = button.Parent;
            StackPanel displayPanel = null;
            StackPanel editPanel = null;
            TextBox editKey = null;
            TextBox editValue = null;

            // 遍历父级控件
            while (parent != null)
            {
                if (parent is Grid grid)
                {
                    // 在 Grid 的子控件中查找
                    foreach (var child in grid.Children)
                    {
                        if (child is StackPanel sp)
                        {
                            if (sp.Name == "DisplayPanel") displayPanel = sp;
                            else if (sp.Name == "EditPanel") editPanel = sp;

                            // 在编辑面板中查找 TextBox
                            if (sp.Name == "EditPanel")
                            {
                                foreach (var innerChild in sp.Children)
                                {
                                    if (innerChild is TextBox tb)
                                    {
                                        if (tb.Name == "EditKey") editKey = tb;
                                        else if (tb.Name == "EditValue") editValue = tb;
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
                parent = parent.Parent;
            }

            if (displayPanel != null && editPanel != null && editKey != null && editValue != null)
            {
                editKey.Text = item.Key;
                editValue.Text = item.Value;
                displayPanel.IsVisible = false;
                editPanel.IsVisible = true;
            }
        }
    }

    // 确认按钮点击事件：联动串口更新键值
    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var item = button?.DataContext as KeyValueItem;
        if (item == null)
        {
            return;
        }

        var parent = button.Parent;
        StackPanel displayPanel = null;
        StackPanel editPanel = null;
        TextBox editKey = null;
        TextBox editValue = null;

        while (parent != null)
        {
            if (parent is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is StackPanel sp)
                    {
                        if (sp.Name == "DisplayPanel") displayPanel = sp;
                        else if (sp.Name == "EditPanel") editPanel = sp;

                        if (sp.Name == "EditPanel")
                        {
                            foreach (var innerChild in sp.Children)
                            {
                                if (innerChild is TextBox tb)
                                {
                                    if (tb.Name == "EditKey") editKey = tb;
                                    else if (tb.Name == "EditValue") editValue = tb;
                                }
                            }
                        }
                    }
                }
                break;
            }
            parent = parent.Parent;
        }

        if (displayPanel == null || editPanel == null || editKey == null || editValue == null)
        {
            return;
        }

        string newKey = editKey.Text ?? string.Empty;
        string newValue = editValue.Text ?? string.Empty;

        // 联动串口：更新键值（若已连接且已知块 ID）
        if (!string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort) && CurrentBlockId >= 0)
        {
            bool ok = await DeviceDataViewModel.Instance.UpdateKeyAsync(CurrentBlockId, newKey, newValue);
            if (!ok)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error,
                    I18nViewModel.Instance.KeyAddFailed, newKey);
                return;
            }

            // 成功后键值对列表与块列表已由视图模型自动刷新，这里只需收起编辑面板
            editPanel.IsVisible = false;
            displayPanel.IsVisible = true;
            return;
        }

        // 未连接设备时，仅本地修改
        item.Key = newKey;
        item.Value = newValue;
        editPanel.IsVisible = false;
        displayPanel.IsVisible = true;
    }

    // 删除按钮点击事件：联动串口删除键值
    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var item = button?.DataContext as KeyValueItem;
        if (item == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(DeviceDataViewModel.Instance.CurrentPort) && CurrentBlockId >= 0)
        {
            bool ok = await DeviceDataViewModel.Instance.DeleteKeyAsync(CurrentBlockId, item.Key);
            if (!ok)
            {
                InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Error,
                    I18nViewModel.Instance.KeyDelFailed, item.Key);
                return;
            }

            // 成功后键值对列表与块列表已由视图模型自动刷新
            return;
        }

        // 未连接设备时，仅本地移除
        DeviceDataViewModel.Instance.KeyValueItems.Remove(item);
    }
}


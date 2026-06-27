using System;
using System.Collections.ObjectModel;
using ArchivumU.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArchivumU.Views.Components;

public partial class KeyValueMgr : UserControl
{
    
    public KeyValueMgr()
    {
        InitializeComponent();
        DataContext = new KeyValueViewModel();
        EmptyPlaceholderINKVMGR.IsVisible = false;

    }
    
    // protected override void OnDataContextChanged(EventArgs e)
    // {
    //     base.OnDataContextChanged(e);
    //         
    //     // 更新按钮文本
    //     if (DataContext is MainWindowViewModel vm)
    //     {
    //         BTNKeyConfig.Content = vm.Confirm;
    //         BTNKeyDel.Content = vm.Del;
    //     }
    // }
    
    
    
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
    
    // 确认按钮点击事件
    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var item = button?.DataContext as KeyValueItem;
        if (item != null)
        {
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
            
            if (displayPanel != null && editPanel != null && editKey != null && editValue != null)
            {
                item.Key = editKey.Text;
                item.Value = editValue.Text;
                editPanel.IsVisible = false;
                displayPanel.IsVisible = true;
            }
        }
    }
    
    // 删除按钮点击事件
    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var item = button?.DataContext as KeyValueItem;
        if (item != null)
        {
            ((KeyValueViewModel)DataContext).KeyValueItems.Remove(item);
        }
    }
}


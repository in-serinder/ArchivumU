using System.Globalization;
using ArchivumU.I18n;
using ArchivumU.ViewModels;
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
            
            
           // 调试用
           // new DeviceClone().Show();
        }


        private void showCloneWindow()
        {
            // Window? ownerWindow = this.FindAncestorOfType<Window>();
            // if (ownerWindow is null)
            //     return;
            //
            // // var mainVm = DataContext as MainWindowViewModel;
            // var cloneWin = new DeviceClone();
            // cloneWin.ShowDialog(ownerWindow);
            new DeviceCloneWindow().Show();
        }
        
        private void MIDevClone_OnClick(object? sender, RoutedEventArgs e)
        {
            showCloneWindow();
        }
    }
}
using System.Diagnostics;
using System.Globalization;
using ArchivumU.I18n;
using ArchivumU.Models;
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
           InfoDialogViewModel.Show(InfoDialogViewModel.InfoType.Success, "Success", "Test");
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
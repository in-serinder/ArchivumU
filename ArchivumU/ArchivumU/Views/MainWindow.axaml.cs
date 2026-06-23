using System.Globalization;
using ArchivumU.I18n;
using ArchivumU.ViewModels;
using Avalonia.Controls;
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
           
        }
        
        
        


    }
}
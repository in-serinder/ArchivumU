using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArchivumU.I18n;
using System.Globalization;

namespace ArchivumU.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public string Greeting { get; } = "Welcome to Avalonia!";

        [RelayCommand]
        public void SetEnglish()
        {
            Debug.WriteLine("SetEnglish");
            Strings.Culture = new CultureInfo("en-US");
            OnPropertyChanged(string.Empty);
        }

        [RelayCommand]
        public void SetChinese()
        {
            Debug.WriteLine("SetChinese");
            Strings.Culture = new CultureInfo("zh-CN");
            OnPropertyChanged(string.Empty);
        }
    }
}
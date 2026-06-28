using ArchivumU.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArchivumU.Views;

public partial class DeviceCloneWindow : Window
{
    public DeviceCloneWindow()
    {
        InitializeComponent();
        DataContext = new CloneViewModel();
    }

    private void BTNDCCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
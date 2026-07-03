using ArchivumU.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArchivumU.Views;

public partial class ProcessDialogWindow : Window
{
    public ProcessDialogWindow()
    {
        InitializeComponent();
        DataContext = new ProcessMsgViewModel();
    }

    public ProcessDialogWindow(ProcessMsgViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
    

    private void BTNPDCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
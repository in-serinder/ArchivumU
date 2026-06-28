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
    
    

    private void BTNPDCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
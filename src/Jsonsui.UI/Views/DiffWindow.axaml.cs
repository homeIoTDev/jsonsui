using Avalonia.Controls;
using Avalonia.Interactivity;
using Jsonsui.UI.ViewModels;

namespace Jsonsui.UI.Views;

public partial class DiffWindow : Window
{
    public DiffWindow()
    {
        InitializeComponent();
    }

    public DiffWindow(DiffViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

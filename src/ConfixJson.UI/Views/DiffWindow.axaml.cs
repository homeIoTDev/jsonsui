using Avalonia.Controls;
using Avalonia.Interactivity;
using ConfixJson.UI.ViewModels;

namespace ConfixJson.UI.Views;

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

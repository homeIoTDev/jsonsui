using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ConfixJson.Core.Models;
using ConfixJson.UI.ViewModels;

namespace ConfixJson.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void FieldRow_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is string[] path && DataContext is MainWindowViewModel vm)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FocusTrace] FieldRow_Tapped path={string.Join("/", path)} e.Source={e.Source?.GetType().Name}");
            vm.FocusFieldCommand.Execute(path);
        }
    }

    private void CardItem_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is CardItem card && DataContext is MainWindowViewModel vm)
        {
            vm.SelectCardCommand.Execute(card.Index);
        }
    }

    private void CardDelete_Tapped(object? sender, TappedEventArgs e)
    {
        // Stop propagation so the card select doesn't fire
        e.Handled = true;
    }

    private void DiffOverlay_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseDiffCommand.Execute(null);
        }
    }

    private void EnumComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.Parent == null) return;
        if (e.RemovedItems.Count == 0) return;
        if (cb.DataContext is FieldRow row && DataContext is MainWindowViewModel vm && e.AddedItems.Count > 0)
        {
            var value = e.AddedItems[0]?.ToString();
            if (value != null)
                vm.ChangeFieldCommand.Execute(new object[] { row.Path, value });
        }
    }

    private void BooleanCheckBox_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.DataContext is not FieldRow row) return;
        if (DataContext is not MainWindowViewModel vm) return;
        var newValue = cb.IsChecked == true;
        if (newValue == row.BoolValue) return;
        vm.ChangeFieldCommand.Execute(new object[] { row.Path, newValue });
    }

    private void NumericUpDown_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (sender is NumericUpDown nud && nud.DataContext is FieldRow row)
            System.Diagnostics.Debug.WriteLine(
                $"[NUD] ValueChanged Field={row.Key} Old={e.OldValue} New={e.NewValue}");
    }

    private void NumericUpDown_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is NumericUpDown nud && nud.DataContext is FieldRow row)
        {
            var focused = Avalonia.Controls.TopLevel.GetTopLevel(nud)?.FocusManager?.GetFocusedElement();
            System.Diagnostics.Debug.WriteLine(
                $"[FocusTrace] NUD GotFocus Field={row.Key} IsKeyboardFocusWithin={nud.IsKeyboardFocusWithin} " +
                $"FocusedNow={focused?.GetType().Name} e.Source={e.Source?.GetType().Name}");
        }
    }

    private void NumericUpDown_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not NumericUpDown nud || nud.Parent == null) return;
        if (nud.DataContext is not FieldRow row) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.IsRefreshing) return;

        var focused = Avalonia.Controls.TopLevel.GetTopLevel(nud)?.FocusManager?.GetFocusedElement();
        System.Diagnostics.Debug.WriteLine(
            $"[FocusTrace] NUD LostFocus Field={row.Key} IsKeyboardFocusWithin={nud.IsKeyboardFocusWithin} " +
            $"Value={nud.Value} e.Source={e.Source?.GetType().Name} FocusedNow={focused?.GetType().Name}");

        if (nud.IsKeyboardFocusWithin) return;
        if (!nud.Value.HasValue) return;
        if (nud.Value == row.OriginalNumericValue) return;

        var value = nud.Value.Value;
        var fieldType = row.FieldType;
        var path = (string[])row.Path.Clone();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            object v = fieldType == "integer" ? (object)(int)value : (double)value;
            vm.ChangeFieldCommand.Execute(new object[] { path, v });
        });
    }

    private void ScalarDetail_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && DataContext is MainWindowViewModel vm)
        {
            vm.SaveScalarDetailCommand.Execute(tb.Text);
        }
    }
}

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Jsonsui.Core.Models;
using Jsonsui.UI.ViewModels;

namespace Jsonsui.UI.Views;

public partial class FieldEditorView : UserControl
{
    public FieldEditorView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? Vm => TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

    private void DrillIntoObject_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FieldRow row } && Vm is { } vm)
            vm.DrillIntoCommand.Execute(row.Path);
    }

    private void DrillIntoArray_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FieldRow row } && Vm is { } vm)
            vm.DrillIntoArrayCommand.Execute(row.Path);
    }

    // --- Nullable / Union type selection ---

    private void NullableTypeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;
        if (row.IsReadOnly) return;

        OpenNullableTypeMenu(vm, btn, row);
    }

    private void OpenNullableTypeMenu(MainWindowViewModel vm, Control target, FieldRow row)
    {
        var types = vm.GetFieldJsonTypes(row.Path);
        if (types is not { Count: > 0 }) return;

        var menu = new ContextMenu
        {
            Placement = PlacementMode.BottomEdgeAlignedRight
        };

        // Einziges Nicht-null-Typ-Angebot: direkt der Typ.
        foreach (var type in types)
        {
            var label = type switch
            {
                "object" => "Create Object",
                "array" => "Create Array",
                "boolean" => "Create Boolean",
                "integer" or "number" => $"Create {type[0].ToString().ToUpperInvariant() + type[1..]}",
                "string" => "Create String",
                _ => $"Create {type}"
            };

            var item = new MenuItem { Header = label };
            var captured = type;
            // Deferred ausführen (wie bei "Set to null"): das ContextMenu muss erst schließen,
            // bevor RefreshUI() die FieldRow-Controls zerstört -> kein "PlatformImpl is null".
            item.Click += (_, _) =>
            {
                var path = (string[])row.Path.Clone();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.SetFieldToType(path, captured),
                    Avalonia.Threading.DispatcherPriority.Background);
            };
            menu.Items.Add(item);
        }

        menu.Open(target);
    }

    private void EnumComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb || cb.Parent == null) return;
        if (e.RemovedItems.Count == 0) return;
        if (cb.DataContext is FieldRow row && Vm is { } vm && e.AddedItems.Count > 0)
        {
            var value = e.AddedItems[0]?.ToString();
            if (value != null)
                vm.ChangeFieldCommand.Execute(new object[] { row.Path, value });
        }
    }

    private void BooleanCheckBox_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;
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
        if (sender is NumericUpDown nud && nud.DataContext is FieldRow row && Vm is { } vm)
        {
            var focused = Avalonia.Controls.TopLevel.GetTopLevel(nud)?.FocusManager?.GetFocusedElement();
            System.Diagnostics.Debug.WriteLine(
                $"[FocusTrace] NUD GotFocus Field={row.Key} IsKeyboardFocusWithin={nud.IsKeyboardFocusWithin} " +
                $"FocusedNow={focused?.GetType().Name} e.Source={e.Source?.GetType().Name}");
            vm.FocusFieldCommand.Execute(row.Path);

            // Enter is marked handled by NumericUpDown.OnKeyDown, so the XAML KeyDown
            // handler never fires. Attach with handledEventsToo so Enter commits directly.
            nud.AddHandler(InputElement.KeyDownEvent, NudKeyDownHandled, RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }

    private void NumericUpDown_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not NumericUpDown nud || nud.Parent == null) return;
        if (nud.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;
        if (vm.IsRefreshing) return;
        nud.RemoveHandler(InputElement.KeyDownEvent, NudKeyDownHandled);
        if (nud.IsKeyboardFocusWithin) return;
        CommitNumeric(nud, row, vm);
    }

    private void NudKeyDownHandled(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is not NumericUpDown nud || nud.DataContext is not FieldRow row) return;
        if (TopLevel.GetTopLevel(nud)?.DataContext is not MainWindowViewModel vm) return;
        CommitNumeric(nud, row, vm);
    }

    private void CommitNumeric(NumericUpDown nud, FieldRow row, MainWindowViewModel vm)
    {
        // Commit from the raw text rather than Value: this guarantees the exact typed
        // number (e.g. 0 or 65536) reaches JSON/Validate(), independent of the control's
        // internal parsing/formatting.
        var text = nud.Text?.Trim();
        decimal value;
        if (string.IsNullOrEmpty(text))
        {
            if (!nud.Value.HasValue) return;
            value = nud.Value.Value;
        }
        else if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            if (!nud.Value.HasValue) return;
            value = nud.Value.Value;
        }

        if (value == row.OriginalNumericValue) return;
        vm.ApplyNumericChange(row, value, row.FieldType == "integer");
    }

    private void EditorField_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control c && c.DataContext is FieldRow row && Vm is { } vm)
            vm.FocusFieldCommand.Execute(row.Path);
    }

    private void EditorScalar_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is FieldRow row && Vm is { } vm)
            CommitScalarText(tb, row, vm);
    }

    private void EditorScalar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is TextBox tb && tb.DataContext is FieldRow row && Vm is { } vm)
            CommitScalarText(tb, row, vm);
    }

    private void CommitScalarText(TextBox tb, FieldRow row, MainWindowViewModel vm)
    {
        if (tb.Text == vm.GetFieldRawValue(row.Path)) return;
        vm.ApplyTextChange(row, tb.Text);
    }
}

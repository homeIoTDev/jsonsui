using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Jsonsui.Core.Models;
using Jsonsui.UI.Localization;
using Jsonsui.UI.ViewModels;

namespace Jsonsui.UI.Views;

public partial class FieldRowView : UserControl
{
    public FieldRowView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? Vm => TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

    private void FieldRow_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is string[] path && Vm is { } vm)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FocusTrace] FieldRow_Tapped path={string.Join("/", path)} e.Source={e.Source?.GetType().Name}");
            vm.FocusFieldCommand.Execute(path);
        }
    }

    private void FieldRowDelete_Tapped(object? sender, TappedEventArgs e)
    {
        // Stop propagation so the FieldRow_Tapped focus handler doesn't target the
        // soon-to-be-deleted property
        e.Handled = true;
    }

    private void FieldRowAction_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (Vm is not { } vm) return;
        if (btn.DataContext is not FieldRow row) return;
        OpenPropertyActionsMenu(vm, btn, row);
    }

    private void FieldRow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Apps && !(e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))) return;
        if (sender is not Border border) return;
        if (Vm is not { } vm) return;
        if (border.DataContext is not FieldRow row) return;
        e.Handled = true;

        // anchor at the ⋮ action button of the focused row, independent of the mouse position
        var actionButton = border.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Classes.Contains("FieldRowDeleteBtn"));
        var target = actionButton ?? (Control)border;

        // open after the key event fully completes so the popup keeps focus
        Avalonia.Threading.Dispatcher.UIThread.Post(() => OpenPropertyActionsMenu(vm, target, row),
            Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OpenPropertyActionsMenu(MainWindowViewModel vm, Control target, FieldRow row)
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.BottomEdgeAlignedRight
        };

        // ContextMenu.PopupKeyUp schließt das Menü, wenn der OpenContextMenu-Hotkey
        // (Shift+F10 / Context-Menü-Taste) losgelassen wird, während das Menü fokussiert
        // ist. Da wir das Menü per Tastatur öffnen, den KeyUp dieses Gestures als handled
        // markieren, damit PopupKeyUp das Menü nicht sofort wieder schließt.
        menu.KeyUp += (_, ke) =>
        {
            if (Avalonia.Application.Current?.PlatformSettings?.HotkeyConfiguration is { } cfg &&
                cfg.OpenContextMenu.Any(g => g.Matches(ke)))
            {
                ke.Handled = true;
            }
        };

        var renameItem = new MenuItem
        {
            Header = Strings.Get("Action_Rename"),
            IsEnabled = !row.IsReadOnly
        };
        renameItem.Click += (_, _) =>
        {
            vm.BeginRename(row);
            FocusFieldNameEditor(row);
        };
        menu.Items.Add(renameItem);

        var deleteItem = new MenuItem
        {
            Header = Strings.Get("Action_Delete"),
            IsEnabled = !row.IsReadOnly
        };
        deleteItem.Command = vm.DeletePropertyCommand;
        deleteItem.CommandParameter = row;
        menu.Items.Add(deleteItem);

        // Set to null – nur wenn das Schema null erlaubt und der aktuelle Wert nicht bereits null ist.
        if (vm.FieldAllowsNull(row.Path) && !vm.FieldIsNull(row.Path))
        {
            var setNullItem = new MenuItem
            {
                Header = Strings.Get("Action_SetNull"),
                IsEnabled = !row.IsReadOnly
            };
            // Deferred ausführen: das ContextMenu muss erst vollständig schließen, bevor
            // RefreshUI() die FieldRow-Controls (inkl. Anker-Button) zerstört. Sonst routet
            // Avalonia Input/Fokus auf ein bereits detached Control -> "PlatformImpl is null".
            setNullItem.Click += (_, _) =>
            {
                var path = (string[])row.Path.Clone();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.SetFieldToType(path, "null"),
                    Avalonia.Threading.DispatcherPriority.Background);
            };
            menu.Items.Add(setNullItem);
        }

        menu.Open(target);
    }

    // --- Inline Rename ---

    private void FieldName_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TextBlock tb) return;
        if (tb.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;
        if (row.IsReadOnly) return;

        e.Handled = true;
        vm.BeginRename(row);
        FocusFieldNameEditor(row);
    }

    private void FocusFieldNameEditor(FieldRow row)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var editor = FindFieldNameEditor(this, row.Path);
            if (editor == null) return;
            editor.Focus();
            editor.SelectAll();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private static TextBox? FindFieldNameEditor(Avalonia.Visual node, string[] path)
    {
        if (node is TextBox tb &&
            tb.Classes.Contains("FieldNameEditor") &&
            tb.DataContext is FieldRow row &&
            row.Path.SequenceEqual(path))
            return tb;

        foreach (var child in node.GetVisualChildren())
        {
            var result = FindFieldNameEditor(child, path);
            if (result != null) return result;
        }
        return null;
    }

    private void FieldNameEditor_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.SelectAll();
    }

    private void FieldNameEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Escape) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;
        e.Handled = true;

        if (e.Key == Key.Enter)
            vm.CommitRename(row);
        else
            vm.CancelRename(row);
    }

    private void FieldNameEditor_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Parent == null) return;
        if (tb.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;
        if (vm.IsRefreshing) return;
        if (tb.IsKeyboardFocusWithin) return;

        // LostFocus commits nothing; it only exits the rename edit mode without a change.
        vm.CancelRename(row);
    }
}

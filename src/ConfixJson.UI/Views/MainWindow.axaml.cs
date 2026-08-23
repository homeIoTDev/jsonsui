using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ConfixJson.Core.Models;
using ConfixJson.UI.ViewModels;

namespace ConfixJson.UI.Views;

public partial class MainWindow : Window
{
    // --- TEMPORARY DIAGNOSTIC INSTRUMENTATION (Phase: Avalonia PlatformImpl investigation) ---
    private static int _diagSeq;
    private static readonly System.Diagnostics.Stopwatch _diagSw = System.Diagnostics.Stopwatch.StartNew();

    private static void Diag(string tag, string msg)
    {
        var line = $"[DIAG][{Interlocked.Increment(ref _diagSeq):000}][{_diagSw.ElapsedMilliseconds:00000}ms] {tag}: {msg}";
        System.Diagnostics.Debug.WriteLine(line);
        Console.WriteLine(line);
    }

    private static string DiagAttached(Visual? v) => v == null ? "<n/a>" : v.IsAttachedToVisualTree().ToString();

    private static string DiagFocus()
    {
        var app = Avalonia.Application.Current;
        if (app?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return "<no-desktop>";
        var w = desktop.MainWindow;
        if (w == null) return "<no-window>";
        var f = w.FocusManager?.GetFocusedElement();
        if (f == null) return "<none>";
        var name = (f as Control)?.Name ?? f.GetType().Name;
        var dcStr = (f as StyledElement)?.DataContext switch
        {
            FieldRow r => $"FieldRow:{r.Key}@{string.Join("/", r.Path)}",
            null => "null",
            var o => o!.GetType().Name
        };
        var tl = TopLevel.GetTopLevel(f as Visual);
        var tlName = tl == null ? "<none>" : tl.GetType().Name;
        return $"{name} dc={dcStr} root={tlName} attached={DiagAttached(f as Visual)}";
    }

    private static void DiagMenuItemPointer(string label, MenuItem item)
    {
        item.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
            Diag($"Item[{label}]", $"PointerPressed src={e.Source?.GetType().Name} handled={e.Handled}"));
        item.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
            Diag($"Item[{label}]", $"PointerReleased src={e.Source?.GetType().Name} handled={e.Handled}"));
    }

    private static string DiagPopupId(MenuBase menu)
    {
        var tl = TopLevel.GetTopLevel(menu);
        if (tl == null) return "<no-top-level>";
        return $"{tl.GetType().Name}#{tl.GetHashCode()} attached={DiagAttached(tl)}";
    }
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

    private void FieldRowDelete_Tapped(object? sender, TappedEventArgs e)
    {
        // Stop propagation so the FieldRow_Tapped focus handler doesn't target the
        // soon-to-be-deleted property
        e.Handled = true;
    }

    private void FieldRowAction_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (btn.DataContext is not FieldRow row) return;
        OpenPropertyActionsMenu(vm, btn, row);
    }

    private void FieldRow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Apps && !(e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))) return;
        if (sender is not Border border) return;
        if (DataContext is not MainWindowViewModel vm) return;
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

        // TEMP DIAG: ContextMenu lifecycle for the ⋮ (property actions) menu.
        Diag("Menu⋮", $"Created row={row.Key} path={string.Join("/", row.Path)} target={target.GetType().Name}");
        menu.Opening += (_, _) => Diag("Menu⋮", $"Opening focus={DiagFocus()}");
        menu.Opened += (_, _) => Diag("Menu⋮", $"Opened popup={DiagPopupId(menu)} focus={DiagFocus()}");
        menu.Closing += (_, _) => Diag("Menu⋮", $"Closing popup={DiagPopupId(menu)} focus={DiagFocus()}");
        menu.Closed += (_, _) => Diag("Menu⋮", $"Closed popup={DiagPopupId(menu)} focus={DiagFocus()}");
        menu.AttachedToVisualTree += (_, _) => Diag("Menu⋮", $"AttachedToVisualTree popup={DiagPopupId(menu)}");
        menu.DetachedFromVisualTree += (_, _) => Diag("Menu⋮", $"DetachedFromVisualTree popup={DiagPopupId(menu)}");

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
            Header = "Rename",
            IsEnabled = !row.IsReadOnly
        };
        DiagMenuItemPointer("Rename", renameItem);
        renameItem.Click += (_, _) =>
        {
            Diag("Item[Rename]", $"Click BEGIN focus={DiagFocus()}");
            vm.BeginRename(row);
            FocusFieldNameEditor(row);
            Diag("Item[Rename]", $"Click END focus={DiagFocus()}");
        };
        menu.Items.Add(renameItem);

        var deleteItem = new MenuItem
        {
            Header = "Delete",
            IsEnabled = !row.IsReadOnly
        };
        DiagMenuItemPointer("Delete", deleteItem);
        deleteItem.Click += (_, _) => Diag("Item[Delete]", $"Click (Command) BEGIN focus={DiagFocus()}");
        deleteItem.Command = vm.DeletePropertyCommand;
        deleteItem.CommandParameter = row;
        menu.Items.Add(deleteItem);

        // Set to null – nur wenn das Schema null erlaubt und der aktuelle Wert nicht bereits null ist.
        if (vm.FieldAllowsNull(row.Path) && !vm.FieldIsNull(row.Path))
        {
            var setNullItem = new MenuItem
            {
                Header = "Set to null",
                IsEnabled = !row.IsReadOnly
            };
            DiagMenuItemPointer("SetNull", setNullItem);
            // Deferred ausführen: das ContextMenu muss erst vollständig schließen, bevor
            // RefreshUI() die FieldRow-Controls (inkl. Anker-Button) zerstört. Sonst routet
            // Avalonia Input/Fokus auf ein bereits detached Control -> "PlatformImpl is null".
            setNullItem.Click += (_, _) =>
            {
                var path = (string[])row.Path.Clone();
                Diag("Item[SetNull]", $"Click BEGIN (posting deferred) focus={DiagFocus()}");
                Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.SetFieldToType(path, "null"),
                    Avalonia.Threading.DispatcherPriority.Background);
                Diag("Item[SetNull]", $"Click END (deferred posted) focus={DiagFocus()}");
            };
            menu.Items.Add(setNullItem);
        }

        menu.Open(target);
        Diag("Menu⋮", $"Open called popup={DiagPopupId(menu)} focus={DiagFocus()}");
    }

    // --- Inline Rename ---

    private void FieldName_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TextBlock tb) return;
        if (tb.DataContext is not FieldRow row) return;
        if (DataContext is not MainWindowViewModel vm) return;
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
        if (DataContext is not MainWindowViewModel vm) return;
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
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.IsRefreshing) return;
        if (tb.IsKeyboardFocusWithin) return;

        // LostFocus commits nothing; it only exits the rename edit mode without a change.
        vm.CancelRename(row);
    }

    // --- Nullable / Union type selection ---

    private void NullableTypeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not FieldRow row) return;
        if (DataContext is not MainWindowViewModel vm) return;
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

        // TEMP DIAG: ContextMenu lifecycle for the nullable-type selection menu.
        Diag("MenuType", $"Created row={row.Key} path={string.Join("/", row.Path)} types={string.Join(",", types)}");
        menu.Opening += (_, _) => Diag("MenuType", $"Opening focus={DiagFocus()}");
        menu.Opened += (_, _) => Diag("MenuType", $"Opened popup={DiagPopupId(menu)} focus={DiagFocus()}");
        menu.Closing += (_, _) => Diag("MenuType", $"Closing popup={DiagPopupId(menu)} focus={DiagFocus()}");
        menu.Closed += (_, _) => Diag("MenuType", $"Closed popup={DiagPopupId(menu)} focus={DiagFocus()}");
        menu.AttachedToVisualTree += (_, _) => Diag("MenuType", $"AttachedToVisualTree popup={DiagPopupId(menu)}");
        menu.DetachedFromVisualTree += (_, _) => Diag("MenuType", $"DetachedFromVisualTree popup={DiagPopupId(menu)}");

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
            DiagMenuItemPointer(label, item);
            // Deferred ausführen (wie bei "Set to null"): das ContextMenu muss erst schließen,
            // bevor RefreshUI() die FieldRow-Controls zerstört -> kein "PlatformImpl is null".
            item.Click += (_, _) =>
            {
                var path = (string[])row.Path.Clone();
                Diag($"Item[{label}]", $"Click BEGIN (posting deferred) focus={DiagFocus()}");
                Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.SetFieldToType(path, captured),
                    Avalonia.Threading.DispatcherPriority.Background);
                Diag($"Item[{label}]", $"Click END (deferred posted) focus={DiagFocus()}");
            };
            menu.Items.Add(item);
        }

        menu.Open(target);
        Diag("MenuType", $"Open called popup={DiagPopupId(menu)} focus={DiagFocus()}");
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
        if (sender is NumericUpDown nud && nud.DataContext is FieldRow row && DataContext is MainWindowViewModel vm)
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
        if (DataContext is not MainWindowViewModel vm) return;
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

    private void ScalarDetail_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && DataContext is MainWindowViewModel vm)
        {
            vm.SaveScalarDetailCommand.Execute(tb.Text);
        }
    }

    private void ScalarDetail_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CaptureScalarEditingPath();
    }

    private void ScalarDetail_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is TextBox tb && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            vm.SaveScalarDetailCommand.Execute(tb.Text);
        }
    }

    private void EditorField_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control c && c.DataContext is FieldRow row && DataContext is MainWindowViewModel vm)
            vm.FocusFieldCommand.Execute(row.Path);
    }

    private void EditorScalar_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is FieldRow row && DataContext is MainWindowViewModel vm)
            CommitScalarText(tb, row, vm);
    }

    private void EditorScalar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is TextBox tb && tb.DataContext is FieldRow row && DataContext is MainWindowViewModel vm)
            CommitScalarText(tb, row, vm);
    }

    private void CommitScalarText(TextBox tb, FieldRow row, MainWindowViewModel vm)
    {
        if (tb.Text == vm.GetFieldRawValue(row.Path)) return;
        vm.ApplyTextChange(row, tb.Text);
    }

    // --- Add Property Flyout ---

    private Flyout? _addPropertyFlyout;
    private Flyout? _customPropertyFlyout;
    private Control? _addPropertyAnchor;

    private void AddProperty_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || DataContext is not MainWindowViewModel vm) return;
        if (!vm.PrepareAddPropertyFlyout()) return;

        _addPropertyAnchor = btn;
        _addPropertyFlyout?.Hide();
        _addPropertyFlyout = BuildAddPropertyFlyout(vm);
        _addPropertyFlyout.ShowAt(btn);
    }

    private Flyout BuildAddPropertyFlyout(MainWindowViewModel vm)
    {
        var root = new StackPanel
        {
            Width = 280,
            MaxHeight = 360,
            Spacing = 4,
            Margin = new Thickness(4)
        };

        root.Children.Add(new TextBlock
        {
            Text = "Add property",
            Classes = { "FlyoutHeader" },
            Margin = new Thickness(4, 2, 4, 6)
        });

        if (!vm.HasAddPropertySchemaItems)
        {
            root.Children.Add(new TextBlock
            {
                Text = "No schema properties available",
                Classes = { "FlyoutEmpty" },
                Margin = new Thickness(4, 2, 4, 2)
            });
        }
        else
        {
            root.Children.Add(new TextBlock
            {
                Text = "Schema properties",
                Classes = { "FlyoutItemType" },
                Margin = new Thickness(4, 2, 4, 2)
            });

            var list = new StackPanel { Spacing = 2 };
            foreach (var item in vm.AddPropertyItems)
            {
                var content = new StackPanel { Spacing = 2 };
                var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                top.Children.Add(new TextBlock { Text = item.Name, Classes = { "FlyoutItemName" } });

                if (!string.IsNullOrEmpty(item.Type) && item.Type != "string")
                    top.Children.Add(new TextBlock
                    {
                        Text = item.Type,
                        Classes = { "FlyoutItemType" },
                        VerticalAlignment = VerticalAlignment.Center
                    });

                if (item.IsRequired)
                    top.Children.Add(new Border
                    {
                        Classes = { "Badge", "BadgeRequired" },
                        Padding = new Thickness(4, 1),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "REQUIRED", Classes = { "BadgeText", "FlyoutBadgeText" } }
                    });

                content.Children.Add(top);

                if (!string.IsNullOrEmpty(item.DefaultValue))
                    content.Children.Add(new TextBlock
                    {
                        Text = $"default {item.DefaultValue}",
                        Classes = { "FlyoutItemType" }
                    });

                var btn = new Button
                {
                    Content = content,
                    Classes = { "FlyoutItem" },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(6, 4)
                };

                var captured = item;
                btn.Click += (_, _) =>
                {
                    _addPropertyFlyout?.Hide();
                    vm.AddSchemaProperty(captured);
                };

                list.Children.Add(btn);
            }

            root.Children.Add(new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = list
            });
        }

        if (vm.CanAddCustomProperty)
        {
            root.Children.Add(new Border
            {
                Classes = { "FlyoutSeparator" },
                Margin = new Thickness(4, 4, 4, 2)
            });

            var customBtn = new Button
            {
                Classes = { "FlyoutItem" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 4),
                Content = new TextBlock { Text = "+ Custom property", Classes = { "FlyoutItemName" } }
            };

            customBtn.Click += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _addPropertyFlyout?.Hide();
                    ShowCustomPropertyFlyout(vm, _addPropertyAnchor ?? this);
                }, Avalonia.Threading.DispatcherPriority.Background);
            };

            root.Children.Add(customBtn);
        }

        return new Flyout
        {
            Content = root,
            Placement = PlacementMode.BottomEdgeAlignedRight
        };
    }

    private void ShowCustomPropertyFlyout(MainWindowViewModel vm, Control anchor)
    {
        var root = new StackPanel { Width = 260, Spacing = 6, Margin = new Thickness(4) };

        root.Children.Add(new TextBlock
        {
            Text = "Custom property",
            Classes = { "FlyoutHeader" },
            Margin = new Thickness(4, 2, 4, 4)
        });

        var nameBox = new TextBox { PlaceholderText = "Property name", Classes = { "EditorInput" } };
        var valueBox = new TextBox { PlaceholderText = "Value", Classes = { "EditorInput" } };
        var addBtn = new Button { Content = "Add", Classes = { "DashedBtn" } };

        void Submit()
        {
            _customPropertyFlyout?.Hide();
            vm.AddCustomProperty(nameBox.Text ?? "", valueBox.Text);
        }

        addBtn.Click += (_, _) => Submit();
        nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Submit();
            }
        };
        valueBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Submit();
            }
        };

        root.Children.Add(new TextBlock
        {
            Text = "Name",
            Classes = { "FlyoutItemType" },
            Margin = new Thickness(4, 2, 4, 0)
        });
        root.Children.Add(nameBox);
        root.Children.Add(new TextBlock
        {
            Text = "Value",
            Classes = { "FlyoutItemType" },
            Margin = new Thickness(4, 4, 4, 0)
        });
        root.Children.Add(valueBox);
        root.Children.Add(addBtn);

        _customPropertyFlyout?.Hide();
        _customPropertyFlyout = new Flyout
        {
            Content = root,
            Placement = PlacementMode.BottomEdgeAlignedRight
        };
        _customPropertyFlyout.ShowAt(anchor);
    }
}

using System.Globalization;
using System.Linq;
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
    public MainWindow()
    {
        InitializeComponent();
    }

    private void FilterBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not TextBox tb) return;
        e.Handled = true;
        tb.Clear();
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
        renameItem.Click += (_, _) =>
        {
            vm.BeginRename(row);
            FocusFieldNameEditor(row);
        };
        menu.Items.Add(renameItem);

        var deleteItem = new MenuItem
        {
            Header = "Delete",
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
                Header = "Set to null",
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

    // --- Error List Flyout ---

    private Flyout? _errorFlyout;

    private void ErrorBadge_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || DataContext is not MainWindowViewModel vm) return;
        if (vm.ErrorItems.Count == 0) return;

        var root = new StackPanel
        {
            Width = 320,
            MaxHeight = 380,
            Spacing = 2,
            Margin = new Thickness(4)
        };

        root.Children.Add(new TextBlock
        {
            Text = $"Errors ({vm.ErrorItems.Count})",
            Classes = { "FlyoutHeader" },
            Margin = new Thickness(4, 2, 4, 6)
        });

        var list = new StackPanel { Spacing = 2 };
        foreach (var item in vm.ErrorItems)
        {
            var content = new StackPanel { Spacing = 2 };
            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            top.Children.Add(new TextBlock
            {
                Text = item.DisplayPath,
                Classes = { "FlyoutItemName" },
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            if (item.IsMissing)
                top.Children.Add(new Border
                {
                    Classes = { "Badge", "BadgeRequired" },
                    Padding = new Thickness(4, 1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = "FEHLT", Classes = { "BadgeText" } }
                });

            content.Children.Add(top);
            content.Children.Add(new TextBlock
            {
                Text = item.Message,
                Classes = { "FlyoutItemType" },
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });

            var rowBtn = new Button
            {
                Content = content,
                Classes = { "FlyoutItem" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 4)
            };

            var captured = item;
            rowBtn.Click += (_, _) =>
            {
                _errorFlyout?.Hide();
                vm.NavigateToError(captured);
            };

            list.Children.Add(rowBtn);
        }

        root.Children.Add(new ScrollViewer
        {
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = list
        });

        _errorFlyout?.Hide();
        _errorFlyout = new Flyout
        {
            Content = root,
            Placement = PlacementMode.BottomEdgeAlignedRight
        };
        _errorFlyout.ShowAt(btn);
    }

    // --- Add Property Flyout ---

    private Flyout? _addPropertyFlyout;
    private Flyout? _customPropertyFlyout;
    private Flyout? _addTypeSelectionFlyout;
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
                    var types = vm.GetItemJsonTypes(captured);
                    if (types is { Count: > 1 })
                    {
                        _addPropertyFlyout?.Hide();
                        ShowAddTypeSelectionFlyout(vm, captured);
                        return;
                    }
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

    // --- Add Property: Typauswahl bei mehrdeutigen Unions (2+ Nicht-null-Typen) ---

    private void ShowAddTypeSelectionFlyout(MainWindowViewModel vm, PropertyCatalogItem item)
    {
        var types = vm.GetItemJsonTypes(item);
        if (types is not { Count: > 1 }) return;

        var root = new StackPanel
        {
            Width = 260,
            Spacing = 2,
            Margin = new Thickness(4)
        };

        root.Children.Add(new TextBlock
        {
            Text = $"Type for \"{item.Name}\"",
            Classes = { "FlyoutHeader" },
            Margin = new Thickness(4, 2, 4, 6)
        });

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

            var btn = new Button
            {
                Classes = { "FlyoutItem" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 4),
                Content = new TextBlock { Text = label, Classes = { "FlyoutItemName" } }
            };

            var capturedType = type;
            btn.Click += (_, _) =>
            {
                _addTypeSelectionFlyout?.Hide();
                vm.AddSchemaProperty(item, capturedType);
            };

            root.Children.Add(btn);
        }

        _addTypeSelectionFlyout?.Hide();
        _addTypeSelectionFlyout = new Flyout
        {
            Content = root,
            Placement = PlacementMode.BottomEdgeAlignedRight
        };
        _addTypeSelectionFlyout.ShowAt(_addPropertyAnchor ?? this);
    }
}

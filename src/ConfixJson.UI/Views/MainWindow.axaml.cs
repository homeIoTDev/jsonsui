using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
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

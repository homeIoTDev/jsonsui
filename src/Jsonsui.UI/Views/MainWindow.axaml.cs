using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Jsonsui.Core.Models;
using Jsonsui.UI.Localization;
using Jsonsui.UI.ViewModels;

namespace Jsonsui.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TopHeaderBar.PointerPressed += TopHeaderBar_PointerPressed;
        Opened += MainWindow_Opened;
    }

    private void MainWindow_Opened(object? sender, System.EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        void FocusInitial()
        {
            if (vm.HasDocument)
                vm.FocusTreeSelection();
            else
                this.FindControl<Button>("OpenFileButton")?.Focus();
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(FocusInitial, Avalonia.Threading.DispatcherPriority.Loaded);
        Avalonia.Threading.Dispatcher.UIThread.Post(FocusInitial, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void TopHeaderBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Do not treat clicks on interactive elements (status/window buttons) as a drag.
        if (e.Source is Visual v && v.FindAncestorOfType<Button>(includeSelf: true) != null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void Close_Click(object? sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void FilterBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not TextBox tb) return;
        e.Handled = true;
        tb.Clear();
        if (DataContext is MainWindowViewModel vm)
            vm.FocusTreeSelection();
    }

    // --- Tree keyboard focus ---

    private void Tree_GotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is ItemsControl && DataContext is MainWindowViewModel vm)
            vm.FocusTreeSelection();
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        switch (e.Key)
        {
            case Key.Up:
                vm.MoveTreeSelectionCommand.Execute(-1);
                break;
            case Key.Down:
                vm.MoveTreeSelectionCommand.Execute(1);
                break;
            case Key.Home:
                vm.TreeHomeCommand.Execute(null);
                break;
            case Key.End:
                vm.TreeEndCommand.Execute(null);
                break;
            case Key.Right:
                vm.ExpandTreeCommand.Execute(null);
                break;
            case Key.Left:
                vm.CollapseTreeCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
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

    private void CardDuplicate_Tapped(object? sender, TappedEventArgs e)
    {
        // Stop propagation so the card select doesn't fire
        e.Handled = true;
    }

    private void Cards_GotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is ItemsControl && DataContext is MainWindowViewModel vm)
            vm.FocusSelectedCard();
    }

    private void CardItem_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Border { DataContext: CardItem card }) return;
        if (DataContext is not MainWindowViewModel vm) return;

        switch (e.Key)
        {
            case Key.Up:
                vm.MoveCardSelectionCommand.Execute(-1);
                break;
            case Key.Down:
                vm.MoveCardSelectionCommand.Execute(1);
                break;
            case Key.Enter:
                vm.SelectCardCommand.Execute(card.Index);
                vm.FocusCardDetail();
                break;
            case Key.Delete:
                vm.DeleteCardCommand.Execute(card.Index);
                break;
            case Key.Insert:
            case Key.Add:
                vm.AddCardCommand.Execute(null);
                break;
            case Key.Escape:
                vm.EscapeFromCardCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && HandleShortcut(vm, e))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private bool IsFocusInDetailPanel()
    {
        if (FocusManager?.GetFocusedElement() is not Visual focused) return false;
        return focused == ArrayDetailPanel || ArrayDetailPanel.IsVisualAncestorOf(focused);
    }

    private bool IsFocusInTree()
    {
        if (FocusManager?.GetFocusedElement() is not Visual focused) return false;
        return focused == TreePanel || TreePanel.IsVisualAncestorOf(focused);
    }

    private bool HandleShortcut(MainWindowViewModel vm, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && shift && e.Key == Key.L)
        {
            vm.FocusTreeFilterCommand.Execute(null);
            return true;
        }

        if (ctrl && !shift)
        {
            switch (e.Key)
            {
                case Key.O:
                    vm.OpenFileCommand.Execute(null);
                    return true;
                case Key.S:
                    vm.SaveCommand.Execute(null);
                    return true;
                case Key.Z:
                    vm.UndoCommand.Execute(null);
                    return true;
                case Key.Y:
                    vm.RedoCommand.Execute(null);
                    return true;
                case Key.D:
                    vm.OpenDiffCommand.Execute(null);
                    return true;
            }
        }

        if (ctrl && shift && e.Key == Key.Z)
        {
            vm.RedoCommand.Execute(null);
            return true;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Left)
        {
            if (vm.NestedCtx != null)
                ExitNestedContext(vm);
            return true;
        }

        if (e.Key == Key.Back)
        {
            if (vm.NestedCtx != null && !IsTextInputFocused())
            {
                ExitNestedContext(vm);
                return true;
            }
            return false;
        }

        if (e.Key == Key.Escape)
        {
            if (vm.Diff.IsOpen)
            {
                vm.CloseDiffCommand.Execute(null);
                return true;
            }

            if (vm.NestedCtx != null)
            {
                ExitNestedContext(vm);
                return true;
            }

            if (vm.IsArrayMode && IsFocusInDetailPanel())
            {
                vm.FocusSelectedCard();
                return true;
            }

            if (vm.HasDocument)
            {
                vm.FocusTreeSelection();
                return true;
            }
        }

        return false;
    }

    /// <summary>Leaves the current nested (drill-down) context, mirroring the back button.
    /// When focus was not in the tree, focus returns to the editor area.</summary>
    private void ExitNestedContext(MainWindowViewModel vm)
    {
        var returnToEditor = !IsFocusInTree();
        vm.ExitNestedArrayCommand.Execute(null);
        if (returnToEditor)
            vm.FocusFirstEditorTarget();
    }

    private bool IsTextInputFocused()
    {
        if (FocusManager?.GetFocusedElement() is not Visual focused) return false;
        return focused.FindAncestorOfType<TextBox>(includeSelf: true) != null
            || focused.FindAncestorOfType<NumericUpDown>(includeSelf: true) != null;
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
            Text = Strings.Format("Errors_Header", vm.ErrorItems.Count),
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
                    Child = new TextBlock { Text = Strings.Get("Error_Missing"), Classes = { "BadgeText" } }
                });

            content.Children.Add(top);
            content.Children.Add(new TextBlock
            {
                Text = Strings.Format(item.Message.Key, item.Message.Args),
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
        var root = new Grid
        {
            Width = 280,
            MaxHeight = 360,
            Margin = new Thickness(4),
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto")
        };

        var header = new TextBlock
        {
            Text = Strings.Get("Property_Add"),
            Classes = { "FlyoutHeader" },
            Margin = new Thickness(4, 2, 4, 6)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var sectionLabel = new TextBlock
        {
            Text = vm.HasAddPropertySchemaItems
                ? Strings.Get("Property_SchemaSection")
                : Strings.Get("Property_NoneAvailable"),
            Classes = { vm.HasAddPropertySchemaItems ? "FlyoutItemType" : "FlyoutEmpty" },
            Margin = new Thickness(4, 2, 4, 2)
        };
        Grid.SetRow(sectionLabel, 1);
        root.Children.Add(sectionLabel);

        if (vm.HasAddPropertySchemaItems)
        {
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
                        Child = new TextBlock { Text = Strings.Get("Badge_Required"), Classes = { "BadgeText", "FlyoutBadgeText" } }
                    });

                content.Children.Add(top);

                if (!string.IsNullOrEmpty(item.DefaultValue))
                    content.Children.Add(new TextBlock
                    {
                        Text = Strings.Format("Property_Default", item.DefaultValue),
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

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = list
            };
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);
        }

        if (vm.CanAddCustomProperty)
        {
            var separator = new Border
            {
                Classes = { "FlyoutSeparator" },
                Margin = new Thickness(4, 4, 4, 2)
            };
            Grid.SetRow(separator, 3);
            root.Children.Add(separator);

            var customBtn = new Button
            {
                Classes = { "FlyoutItem" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 4),
                Content = new TextBlock { Text = Strings.Get("Property_AddCustom"), Classes = { "FlyoutItemName" } }
            };

            customBtn.Click += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _addPropertyFlyout?.Hide();
                    ShowCustomPropertyFlyout(vm, _addPropertyAnchor ?? this);
                }, Avalonia.Threading.DispatcherPriority.Background);
            };

            Grid.SetRow(customBtn, 4);
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
            Text = Strings.Get("Property_CustomTitle"),
            Classes = { "FlyoutHeader" },
            Margin = new Thickness(4, 2, 4, 4)
        });

        var nameBox = new TextBox { PlaceholderText = Strings.Get("Property_NamePlaceholder"), Classes = { "EditorInput" } };
        var valueBox = new TextBox { PlaceholderText = "Value", Classes = { "EditorInput" } };
        var addBtn = new Button { Content = Strings.Get("Action_Add"), Classes = { "DashedBtn" } };

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

    // --- Add Property: type selection for ambiguous unions (2+ non-null types) ---

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
            Text = Strings.Format("Property_TypeFor", item.Name),
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ConfixJson.UI.Models;
using ConfixJson.UI.ViewModels;

namespace ConfixJson.UI.Views;

public class TreeDataTemplate : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is not JsonEditorNode node)
            return null;

        var indent = node.Depth * 14.0 + 8.0;

        var rootBorder = new Border
        {
            Padding = new Thickness(indent, 1, 8, 1),
            MinHeight = 24,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = Brushes.Transparent
        };

        // Selection state
        rootBorder.AttachedToVisualTree += (s, e) =>
        {
            var border = (Border)s!;
            if (border.DataContext is JsonEditorNode n)
            {
                border.Classes.Set("Selected", n.IsSelected);
                if (n.IsSelected)
                    border.BorderBrush = Application.Current!.Resources["SelectedBorderBrush"] as IBrush;
            }
        };

        // Hover effect
        rootBorder.PointerEntered += (_, _) =>
        {
            if (rootBorder.DataContext is JsonEditorNode n && !n.IsSelected)
                rootBorder.Background = Application.Current!.Resources["TreeHoverBrush"] as IBrush;
        };
        rootBorder.PointerExited += (_, _) =>
        {
            rootBorder.Background = Brushes.Transparent;
        };

        // Click to select + toggle
        rootBorder.PointerPressed += (_, _) =>
        {
            if (rootBorder.DataContext is JsonEditorNode n)
            {
                var topLevel = TopLevel.GetTopLevel(rootBorder);
                if (topLevel?.DataContext is MainWindowViewModel vm)
                {
                    if (n.IsExpandable && !n.IsExpanded)
                        vm.ToggleExpandCommand.Execute(n);
                    vm.SelectNodeCommand.Execute(n);
                }
            }
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Chevron
        var chevron = new PathIcon
        {
            Width = 10,
            Height = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (node.IsExpandable)
        {
            chevron.Data = node.IsExpanded
                ? Application.Current!.Resources["GeomChevronDown"] as StreamGeometry
                : Application.Current!.Resources["GeomChevron"] as StreamGeometry;
            chevron.Foreground = Application.Current!.Resources["TextTertiaryBrush"] as IBrush;
        }
        row.Children.Add(chevron);

        // Array bracket icon
        if (node.NodeType == "array")
        {
            row.Children.Add(new PathIcon
            {
                Width = 10, Height = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Application.Current!.Resources["GeomBracket"] as StreamGeometry,
                Foreground = Application.Current!.Resources["AccentBrush"] as IBrush
            });
        }
        else if (node.NodeType == "scalar")
        {
            row.Children.Add(new Ellipse
            {
                Width = 6, Height = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = Application.Current!.Resources["TextTertiaryBrush"] as IBrush
            });
        }

        // Label
        var label = new TextBlock
        {
            Text = node.Label,
            FontFamily = "avares://ConfixJson.UI/Assets#JetBrains Mono, monospace",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (node.NodeType == "root")
        {
            label.Foreground = Application.Current!.Resources["AccentBrush"] as IBrush;
            label.FontWeight = FontWeight.Bold;
        }
        else if (node.NodeType == "object")
        {
            label.FontWeight = FontWeight.Bold;
        }
        row.Children.Add(label);

        // Type tag
        if (node.NodeType is "object" or "array" or "scalar")
        {
            row.Children.Add(new TextBlock
            {
                Text = node.TypeTag,
                FontFamily = "avares://ConfixJson.UI/Assets#JetBrains Mono, monospace",
                FontSize = 10,
                Foreground = Application.Current!.Resources["TextTertiaryBrush"] as IBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });
        }

        // Error icon
        var errorIcon = new PathIcon
        {
            Width = 10, Height = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Data = Application.Current!.Resources["GeomError"] as StreamGeometry,
            Foreground = Application.Current!.Resources["ErrorBrush"] as IBrush,
            Margin = new Thickness(4, 0, 0, 0)
        };
        errorIcon.IsVisible = node.HasErrors;
        node.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(JsonEditorNode.HasErrors))
                errorIcon.IsVisible = node.HasErrors;
        };
        row.Children.Add(errorIcon);

        rootBorder.Child = row;

        if (node.IsExpandable)
        {
            var wrapper = new StackPanel();
            var childrenControl = new ItemsControl
            {
                ItemsSource = node.Children,
                IsVisible = node.IsExpanded
            };
            childrenControl.ItemTemplate = new TreeDataTemplate();

            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(JsonEditorNode.IsExpanded))
                    childrenControl.IsVisible = node.IsExpanded;
            };

            wrapper.Children.Add(rootBorder);
            wrapper.Children.Add(childrenControl);
            return wrapper;
        }

        return rootBorder;
    }

    public bool Match(object? data) => data is JsonEditorNode;
}

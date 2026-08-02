using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ConfixJson.Core.Models;
using ConfixJson.UI.ViewModels;

namespace ConfixJson.UI.Views;

public class TreeDataTemplate : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is not JsonEditorNode node)
            return null;

        var indent = node.Depth * 14.0 + 8.0;
        var isExpanded = node.IsExpanded;
        var isSelected = node.IsSelected;
        var hasErrors = node.HasErrors;

        var rootBorder = new Border
        {
            Padding = new Thickness(indent, 1, 8, 1),
            MinHeight = 24,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = isSelected ? Res<IBrush>("SelectedBorderBrush") : Brushes.Transparent,
            Background = Brushes.Transparent
        };

        if (isSelected)
            rootBorder.Background = Res<IBrush>("SelectedBackgroundBrush") ?? Brushes.Magenta;

        rootBorder.PointerEntered += (_, _) =>
        {
            if (!isSelected)
                rootBorder.Background = Res<IBrush>("TreeHoverBrush") ?? Brushes.Yellow;
        };
        rootBorder.PointerExited += (_, _) =>
        {
            rootBorder.Background = isSelected ? (Res<IBrush>("SelectedBackgroundBrush") ?? Brushes.Magenta) : Brushes.Transparent;
        };

        rootBorder.PointerPressed += (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(rootBorder);
            if (topLevel?.DataContext is MainWindowViewModel vm)
            {
                if (node.IsExpandable && !node.IsExpanded)
                    vm.ToggleExpandCommand.Execute(node);
                vm.SelectNodeCommand.Execute(node);
            }
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Chevron
        var chevron = new Path
        {
            Width = 10, Height = 10,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent
        };

        if (node.IsExpandable)
        {
            chevron.Data = isExpanded
                ? Res<StreamGeometry>("feather-chevron-down")
                : Res<StreamGeometry>("feather-chevron-right");
            chevron.Stroke = Res<IBrush>("TextTertiaryBrush");
        }
        row.Children.Add(chevron);

        if (node.NodeType == "array")
        {
            row.Children.Add(new Path
            {
                Width = 10, Height = 10,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Res<StreamGeometry>("feather-bracket"),
                Stroke = Res<IBrush>("AccentBrush"),
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            });
        }
        else if (node.NodeType == "scalar")
        {
            row.Children.Add(new Ellipse
            {
                Width = 6, Height = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = Res<IBrush>("TextTertiaryBrush")
            });
        }

        var label = new TextBlock
        {
            Text = node.Label,
            FontFamily = Res<FontFamily>("FontJetBrainsMono") ?? FontFamily.Default,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (node.NodeType == "root")
        {
            label.Foreground = Res<IBrush>("AccentBrush");
            label.FontWeight = FontWeight.Bold;
        }
        else if (node.NodeType == "object")
        {
            label.FontWeight = FontWeight.Bold;
        }
        row.Children.Add(label);

        if (node.NodeType is "object" or "array" or "scalar")
        {
            row.Children.Add(new TextBlock
            {
                Text = node.TypeTag,
                FontFamily = Res<FontFamily>("FontJetBrainsMono") ?? FontFamily.Default,
                FontSize = 11,
                Foreground = Res<IBrush>("TextTertiaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });
        }

        var errorIcon = new Path
        {
            Width = 10, Height = 10,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Data = Res<StreamGeometry>("feather-alert-circle"),
            Stroke = Res<IBrush>("ErrorBrush"),
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            Margin = new Thickness(4, 0, 0, 0),
            IsVisible = hasErrors
        };
        row.Children.Add(errorIcon);

        rootBorder.Child = row;

        if (node.IsExpandable)
        {
            var wrapper = new StackPanel();
            var childrenControl = new ItemsControl
            {
                ItemsSource = node.Children,
                IsVisible = isExpanded
            };
            childrenControl.ItemTemplate = new TreeDataTemplate();
            wrapper.Children.Add(rootBorder);
            wrapper.Children.Add(childrenControl);
            return wrapper;
        }

        return rootBorder;
    }

    public bool Match(object? data) => data is JsonEditorNode;

    private static T? Res<T>(string key) where T : class
    {
        var app = Application.Current;
        if (app == null) return null;

        var theme = app.RequestedThemeVariant;

        // Use the resource host interface for full theme-aware lookup
        if (((Avalonia.Controls.IResourceHost)app).TryFindResource(key, theme, out var value))
            return value as T;

        return null;
    }
}

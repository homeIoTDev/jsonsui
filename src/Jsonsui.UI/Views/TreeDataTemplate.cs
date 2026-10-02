using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.Text.Json.Nodes;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.UI.Localization;
using Jsonsui.UI.ViewModels;

namespace Jsonsui.UI.Views;

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
            Focusable = true,
            IsTabStop = false,
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
                if (node.IsExpandable)
                    vm.ToggleExpandCommand.Execute(node);
                vm.SelectNodeCommand.Execute(node);
            }
        };

        rootBorder.GotFocus += (_, _) =>
        {
            if (!isSelected)
                rootBorder.BorderBrush = Res<IBrush>("AccentBrush") ?? Brushes.Transparent;
        };
        rootBorder.LostFocus += (_, _) =>
        {
            if (!isSelected)
                rootBorder.BorderBrush = Brushes.Transparent;
        };

        rootBorder.KeyDown += (_, e) => HandleTreeKey(node, rootBorder, e);

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
                ? Res<StreamGeometry>("lucide-chevron-down")
                : Res<StreamGeometry>("lucide-chevron-right");
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
                Data = Res<StreamGeometry>("array-bracket"),
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
            FontSize = ResFontSize("EditorFontSizeBase", 14),
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
                Text = TagFor(node),
                FontFamily = Res<FontFamily>("FontJetBrainsMono") ?? FontFamily.Default,
                FontSize = ResFontSize("EditorFontSizeBase", 14),
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
            Data = Res<StreamGeometry>("lucide-circle-alert"),
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
                ItemsSource = node.NavigableChildren,
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

    private static void HandleTreeKey(JsonEditorNode node, Control source, KeyEventArgs e)
    {
        if (TopLevel.GetTopLevel(source)?.DataContext is not MainWindowViewModel vm) return;

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
            case Key.Enter:
                vm.ActivateTreeNodeCommand.Execute(node);
                break;
            case Key.Space:
                if (node.IsExpandable)
                    vm.ToggleExpandCommand.Execute(node);
                else
                    vm.SelectNodeCommand.Execute(node);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private static string TagFor(JsonEditorNode node) => node.NodeType switch
    {
        "object" => Strings.Format("Tree_ObjectTag", node.Children.Count),
        "array" => Strings.Format("Tree_ArrayTag", (node.Value as JsonArray)?.Count ?? 0),
        _ => JsonDocumentService.GetScalarPreview(node.Value)
    };

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

    private static double ResFontSize(string key, double fallback)
    {
        var app = Application.Current;
        if (app != null && ((Avalonia.Controls.IResourceHost)app).TryFindResource(key, app.RequestedThemeVariant, out var value))
            return value is double d ? d : fallback;

        return fallback;
    }
}

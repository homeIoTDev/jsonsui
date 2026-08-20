using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ConfixJson.Core.Models;

namespace ConfixJson.UI.Converters;

public static class EditorConverters
{
    public static readonly IValueConverter EditorModeToBool = new FuncValueConverter<string, string, bool>((fieldType, target) =>
        fieldType == target
    );

    public static readonly IValueConverter FieldTypeIsTemporal = new FuncValueConverter<string, bool>(fieldType =>
        fieldType is "date" or "time" or "date-time"
    );

    public static readonly IValueConverter FieldTypeAny = new FuncValueConverter<string, string, bool>((fieldType, targets) =>
        targets?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(fieldType) == true
    );

    public static readonly IValueConverter BoolToVisibility = new FuncValueConverter<bool, bool>(b => b);

    public static readonly IValueConverter InverseBool = new FuncValueConverter<bool, bool>(b => !b);

    public static readonly IValueConverter NullToBool = new FuncValueConverter<object?, bool>(o => o != null);

    public static readonly IValueConverter NullToInverseBool = new FuncValueConverter<object?, bool>(o => o == null);

    public static readonly IValueConverter DepthToMargin = new FuncValueConverter<int, double>(depth => depth * 14.0 + 8.0);

    public static readonly IValueConverter NodeTypeToIcon = new FuncValueConverter<string, string>(type => type switch
    {
        "root" => "M6 4 L10 8 L6 12",
        "object" => "M6 4 L10 8 L6 12",
        "array" => "M4 4 L4 12 M4 4 L6 4 M4 8 L6 8 M4 12 L6 12 M10 12 L10 4 M8 4 L10 4 M8 8 L10 8 M8 12 L10 12",
        _ => ""
    });

    public static readonly IValueConverter BoolToChevronRotation = new FuncValueConverter<bool, double>(expanded => expanded ? 90 : 0);

    public static readonly IValueConverter ErrorToOpacity = new FuncValueConverter<bool, double>(hasError => hasError ? 1.0 : 0.0);

    public static readonly IValueConverter DiffLineTypeToBackground = new FuncValueConverter<string, string>(type => type switch
    {
        "removed" => "ErrorTinted",
        "added" => "AccentTinted",
        _ => "Transparent"
    });

    public static readonly IValueConverter DiffLineTypeToMarker = new FuncValueConverter<string, string>(type => type switch
    {
        "removed" => "−",
        "added" => "+",
        _ => " "
    });

    public static readonly IValueConverter DiffLineTypeToMarkerColor = new FuncValueConverter<string, string>(type => type switch
    {
        "removed" => "Error",
        "added" => "Accent",
        _ => "Dim"
    });

    public static readonly IValueConverter CardSelectedToBorder = new FuncValueConverter<bool, string>(selected =>
        selected ? "SelectedBorder" : "BorderSubtle");

    public static readonly IValueConverter CardSelectedToBorderBrush = new FuncValueConverter<bool, IBrush?>(selected =>
    {
        var app = Avalonia.Application.Current;
        if (app == null) return null;

        var key = selected ? "SelectedBorderBrush" : "PanelBorderBrush";
        var theme = app.RequestedThemeVariant;
        if (((IResourceHost)app).TryFindResource(key, theme, out var value) && value is IBrush brush)
            return brush;
        return null;
    });

    public static readonly IValueConverter CardHasErrorToBorder = new FuncValueConverter<bool, string>(hasError =>
        hasError ? "ErrorBorder" : "BorderSubtle");

    public static readonly IValueConverter FileNameToTitle = new FuncValueConverter<string, string>(name =>
        string.IsNullOrEmpty(name) ? "JSON EDITOR" : $"JSON EDITOR — {name}");
}

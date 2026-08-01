using CommunityToolkit.Mvvm.ComponentModel;

namespace ConfixJson.UI.Models;

public sealed partial class SchemaFieldInfo : ObservableObject
{
    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial string? DefaultValue { get; set; }

    [ObservableProperty]
    public partial bool IsDeprecated { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial string? Comment { get; set; }

    [ObservableProperty]
    public partial double? Minimum { get; set; }

    [ObservableProperty]
    public partial double? Maximum { get; set; }

    [ObservableProperty]
    public partial string? PathString { get; set; }

    [ObservableProperty]
    public partial bool HasValidationErrors { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessages { get; set; }

    public string RangeText =>
        Minimum.HasValue && Maximum.HasValue ? $"range: {Minimum} – {Maximum}" :
        Minimum.HasValue ? $"min: {Minimum}" :
        Maximum.HasValue ? $"max: {Maximum}" : "";

    public bool HasRange => Minimum.HasValue || Maximum.HasValue;
}

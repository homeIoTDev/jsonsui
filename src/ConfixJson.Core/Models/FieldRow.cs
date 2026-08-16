namespace ConfixJson.Core.Models;

public class FieldRow
{
    public string Key { get; set; } = "";
    public string[] Path { get; set; } = [];
    public string FieldType { get; set; } = "scalar";
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public bool IsDeprecated { get; set; }
    public bool IsReadOnly { get; set; }
    public bool HasErrors { get; set; }
    public string? DefaultValue { get; set; }
    public string? Comment { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string? NestedObjectSummary { get; set; }
    public string ArrayItemCount { get; set; } = "0";
    public string? ScalarValue { get; set; }
    public List<string>? EnumValues { get; set; }
    public bool BoolValue { get; set; }
    public decimal? NumericValue { get; set; }
    public decimal? OriginalNumericValue { get; set; }
    public decimal MinValue { get; set; } = decimal.MinValue;
    public decimal MaxValue { get; set; } = decimal.MaxValue;
    public DateTimeOffset? DateValue { get; set; }
    public TimeSpan? TimeValue { get; set; }
    public bool UseSeconds { get; set; }
    public bool HasOffset { get; set; }
    public TimeSpan Offset { get; set; }
    public bool IsUtcSuffix { get; set; }
}

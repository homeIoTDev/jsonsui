namespace ConfixJson.Core.Models;

public class SchemaFieldInfo
{
    public string? Description { get; set; }
    public string? ContextDescription { get; set; }
    public string? ContextMeta { get; set; }
    public string? FieldDescription { get; set; }
    public string? FieldMeta { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsDeprecated { get; set; }
    public bool IsReadOnly { get; set; }
    public string? Comment { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string? PathString { get; set; }
    public bool HasValidationErrors { get; set; }
    public string? ErrorMessages { get; set; }

    public string RangeText =>
        Minimum.HasValue && Maximum.HasValue ? $"range: {Minimum} – {Maximum}" :
        Minimum.HasValue ? $"min: {Minimum}" :
        Maximum.HasValue ? $"max: {Maximum}" : "";

    public bool HasRange => Minimum.HasValue || Maximum.HasValue;
}

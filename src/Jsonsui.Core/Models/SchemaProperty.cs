namespace Jsonsui.Core.Models;

public class SchemaProperty
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsDeprecated { get; set; }
    public string? Comment { get; set; }
    public UiElementType UiType { get; set; }
    public string? Format { get; set; }
    public string? Const { get; set; }

    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }

    public List<string>? EnumValues { get; set; }

    public SchemaModel? ObjectSchema { get; set; }

    public SchemaProperty? ArrayItemSchema { get; set; }

    /// <summary>
    /// Effective (non-null) type of the field. For "type": ["object","null"] this
    /// is "object"; "null" is tracked separately via <see cref="IsNullable"/>.
    /// null = the type array contains 0 non-null types (e.g. ["null"]).
    /// </summary>
    public string? JsonType { get; set; }

    /// <summary>
    /// true if the type was specified as an array containing "null"
    /// (e.g. ["string","null"]). The effective type is in <see cref="JsonType"/>.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// All non-null types from a "type" array, if present.
    /// Empty list = type array without non-null types (e.g. ["null"]).
    /// null = "type" was a single string.
    /// </summary>
    public List<string>? JsonTypes { get; set; }
}

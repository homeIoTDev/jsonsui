namespace ConfixJson.Core.Models;

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
    /// Effektiver (Nicht-null-)Typ des Felds. Bei "type": ["object","null"] ist
    /// dies "object"; "null" wird separat über <see cref="IsNullable"/> erfasst.
    /// </summary>
    public string JsonType { get; set; } = "string";

    /// <summary>
    /// true, wenn der Typ als Array mit "null" angegeben wurde
    /// (z. B. ["string","null"]). Der effektive Typ steht in <see cref="JsonType"/>.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Alle Nicht-null-Typen aus einem "type"-Array, falls vorhanden.
    /// null = "type" war ein einzelner String.
    /// </summary>
    public List<string>? JsonTypes { get; set; }
}

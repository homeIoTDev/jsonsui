namespace Jsonsui.Core.Models;

public class SchemaModel
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? SchemaUri { get; set; }
    public List<SchemaProperty> Properties { get; set; } = [];
    public List<string> Required { get; set; } = [];

    /// <summary>
    /// State of "additionalProperties" for this object schema:
    /// null = not specified, true = allowed, false = forbidden.
    /// If an additional schema is present, it is stored in
    /// <see cref="AdditionalPropertiesSchema"/> and this field stays null.
    /// </summary>
    public bool? AdditionalPropertiesAllowed { get; set; }

    /// <summary>
    /// Schema for additional properties ("additionalProperties": { ... }).
    /// null = no schema specified.
    /// </summary>
    public SchemaProperty? AdditionalPropertiesSchema { get; set; }
}

namespace Jsonsui.Core.Models;

public class SchemaModel
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? SchemaUri { get; set; }
    public List<SchemaProperty> Properties { get; set; } = [];
    public List<string> Required { get; set; } = [];

    /// <summary>
    /// Zustand von "additionalProperties" für dieses Object-Schema:
    /// null = nicht angegeben, true = erlaubt, false = verboten.
    /// Ist ein zusätzliches Schema vorhanden, steht es in <see cref="AdditionalPropertiesSchema"/>
    /// und dieses Feld bleibt null.
    /// </summary>
    public bool? AdditionalPropertiesAllowed { get; set; }

    /// <summary>
    /// Schema für zusätzliche Properties ("additionalProperties": { ... }).
    /// null = kein Schema angegeben.
    /// </summary>
    public SchemaProperty? AdditionalPropertiesSchema { get; set; }
}

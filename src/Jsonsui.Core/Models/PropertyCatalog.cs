namespace Jsonsui.Core.Models;

/// <summary>
/// UI-neutral, flat description of a property that is defined in the schema but not
/// yet present and can be added to an object context.
/// </summary>
public sealed class PropertyCatalogItem
{
    public string Name { get; init; } = "";
    public string? Type { get; init; }
    public bool IsRequired { get; init; }
    public bool IsAlreadyPresent { get; init; }
    public bool IsCustom { get; init; }
    public string? DefaultValue { get; init; }
    public string? ConstValue { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsDeprecated { get; init; }
    public string? Description { get; init; }
    public string? Comment { get; init; }
    public List<string>? EnumValues { get; init; }
    public bool CanAdd { get; init; }

    /// <summary>
    /// Access to the full schema property (constraints, format, etc.) in case
    /// the UI needs more details than the flat fields provide.
    /// </summary>
    public SchemaProperty? SchemaProperty { get; init; }
}

/// <summary>
/// Result of the query "which properties can be added to an object context?" –
/// the data source for the add-property flyout.
/// </summary>
public sealed class PropertyCatalog
{
    public bool SchemaAvailable { get; set; }
    public List<PropertyCatalogItem> Items { get; } = [];

    /// <summary>
    /// Raw additionalProperties state of the object schema (phase-1 modeling):
    /// null = not specified, true/false = explicit, for the schema case see
    /// <see cref="AdditionalPropertiesSchema"/>.
    /// </summary>
    public bool? AdditionalPropertiesAllowed { get; set; }

    /// <summary>
    /// Schema for additional properties ("additionalProperties": { ... }), if present.
    /// </summary>
    public SchemaProperty? AdditionalPropertiesSchema { get; set; }

    /// <summary>
    /// Convenience bool for the UI. Rules:
    /// - no schema known -> true (generic custom editing)
    /// - additionalProperties: false -> false
    /// - additionalProperties: true -> true
    /// - additionalProperties: schema -> true
    /// - not specified -> true (JSON Schema default; the decision is open,
    ///   see the raw field <see cref="AdditionalPropertiesAllowed"/>)
    /// </summary>
    public bool CanAddCustomProperty { get; set; }
}

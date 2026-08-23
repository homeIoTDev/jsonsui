namespace ConfixJson.Core.Models;

/// <summary>
/// UI-neutrale, flache Beschreibung eines im Schema definierten, aktuell noch nicht
/// vorhandenen Properties, das an einem Object-Kontext angelegt werden kann.
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
    /// Zugriff auf die vollständige Schema-Property (Constraints, Format usw.),
    /// falls die UI über die flachen Felder hinaus Details benötigt.
    /// </summary>
    public SchemaProperty? SchemaProperty { get; init; }
}

/// <summary>
/// Ergebnis der Abfrage "Welche Properties können an einem Object-Kontext
/// hinzugefügt werden?" – die Datenbasis für ein späteres Add-Property-Flyout.
/// </summary>
public sealed class PropertyCatalog
{
    public bool SchemaAvailable { get; set; }
    public List<PropertyCatalogItem> Items { get; } = [];

    /// <summary>
    /// Roher zusätzlicheProperties-Zustand des Object-Schemas (Phase-1-Modellierung):
    /// null = nicht angegeben, true/false = explizit, bei Schema-Fall siehe
    /// <see cref="AdditionalPropertiesSchema"/>.
    /// </summary>
    public bool? AdditionalPropertiesAllowed { get; set; }

    /// <summary>
    /// Schema für zusätzliche Properties ("additionalProperties": { ... }), falls vorhanden.
    /// </summary>
    public SchemaProperty? AdditionalPropertiesSchema { get; set; }

    /// <summary>
    /// Bequemer bool für die UI. Regeln:
    /// - kein Schema bekannt -> true (generisches Custom-Editing)
    /// - additionalProperties: false -> false
    /// - additionalProperties: true -> true
    /// - additionalProperties: Schema -> true
    /// - nicht angegeben -> true (JSON-Schema-Standard; Entscheidung ist offen,
    ///   siehe rohes Feld <see cref="AdditionalPropertiesAllowed"/>)
    /// </summary>
    public bool CanAddCustomProperty { get; set; }
}

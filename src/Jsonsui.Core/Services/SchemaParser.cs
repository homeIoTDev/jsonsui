using System.Text.Json;
using Jsonsui.Core.Models;

namespace Jsonsui.Core.Services;

public class SchemaParser
{
    private const int MaxRecursionDepth = 32;

    /// <summary>
    /// Root-level "definitions" of this schema. Used to resolve local
    /// "$ref": "#/definitions/&lt;Name&gt;" references.
    /// </summary>
    private Dictionary<string, JsonElement> _definitions = new();

    private int _recursionDepth;

    public SchemaModel Parse(JsonDocument schema)
    {
        var root = schema.RootElement;
        var model = new SchemaModel();

        LoadDefinitions(root);

        if (root.TryGetProperty("title", out var title))
            model.Title = title.GetString();

        if (root.TryGetProperty("description", out var desc))
            model.Description = desc.GetString();

        if (root.TryGetProperty("$schema", out var schemaUri))
            model.SchemaUri = schemaUri.GetString();

        if (root.TryGetProperty("required", out var requiredArray) && requiredArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredArray.EnumerateArray())
            {
                var name = item.GetString();
                if (name is not null)
                    model.Required.Add(name);
            }
        }

        if (root.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in properties.EnumerateObject())
            {
                var schemaProp = ParseProperty(prop.Name, prop.Value);
                schemaProp.IsRequired = model.Required.Contains(prop.Name);
                model.Properties.Add(schemaProp);
            }
        }

        ParseAdditionalProperties(model, root);

        return model;
    }

    /// <summary>
    /// Parses an object schema (the "properties"/"required"/"additionalProperties"
    /// of any schema level) with access to the existing definitions.
    /// </summary>
    private SchemaModel ParseObjectSchema(JsonElement element)
    {
        if (_recursionDepth >= MaxRecursionDepth)
            return new SchemaModel();

        _recursionDepth++;
        try
        {
            var model = new SchemaModel();

            if (element.TryGetProperty("description", out var desc))
                model.Description = desc.GetString();

            if (element.TryGetProperty("required", out var requiredArray) && requiredArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in requiredArray.EnumerateArray())
                {
                    var name = item.GetString();
                    if (name is not null)
                        model.Required.Add(name);
                }
            }

            if (element.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in properties.EnumerateObject())
                {
                    var schemaProp = ParseProperty(prop.Name, prop.Value);
                    schemaProp.IsRequired = model.Required.Contains(prop.Name);
                    model.Properties.Add(schemaProp);
                }
            }

            ParseAdditionalProperties(model, element);

            return model;
        }
        finally
        {
            _recursionDepth--;
        }
    }

    private void LoadDefinitions(JsonElement root)
    {
        _definitions = new Dictionary<string, JsonElement>();
        if (!root.TryGetProperty("definitions", out var definitions) || definitions.ValueKind != JsonValueKind.Object)
            return;

        foreach (var def in definitions.EnumerateObject())
            _definitions[def.Name] = def.Value.Clone();
    }

    private JsonElement? ResolveRef(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
            return null;

        const string prefix = "#/definitions/";
        if (!reference.StartsWith(prefix))
            return null;

        var name = reference.Substring(prefix.Length);
        return _definitions.TryGetValue(name, out var def) ? def : null;
    }

    private void ParseAdditionalProperties(SchemaModel model, JsonElement element)
    {
        if (!element.TryGetProperty("additionalProperties", out var ap)) return;

        switch (ap.ValueKind)
        {
            case JsonValueKind.False:
                model.AdditionalPropertiesAllowed = false;
                break;
            case JsonValueKind.True:
                model.AdditionalPropertiesAllowed = true;
                break;
            case JsonValueKind.Object:
                model.AdditionalPropertiesSchema = ParseProperty("additionalProperties", ap);
                break;
        }
    }

    private SchemaProperty ParseProperty(string name, JsonElement element)
    {
        var prop = new SchemaProperty { Name = name };

        if (element.TryGetProperty("$ref", out var refProp) && refProp.ValueKind == JsonValueKind.String)
        {
            var resolved = ResolveRef(refProp.GetString());
            if (resolved != null)
                element = resolved.Value;
        }

        ParseType(prop, element);

        if (element.TryGetProperty("description", out var desc))
            prop.Description = desc.GetString();

        if (element.TryGetProperty("default", out var defaultVal))
            prop.DefaultValue = defaultVal.ValueKind != JsonValueKind.Null ? defaultVal.ToString() : null;

        if (element.TryGetProperty("const", out var constVal) && constVal.ValueKind != JsonValueKind.Null)
        {
            prop.Const = constVal.ValueKind switch
            {
                JsonValueKind.String => constVal.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => constVal.ToString()
            };
        }

        if (element.TryGetProperty("readOnly", out var readOnly))
            prop.IsReadOnly = readOnly.ValueKind == JsonValueKind.True;

        if (element.TryGetProperty("deprecated", out var deprecated))
            prop.IsDeprecated = deprecated.ValueKind == JsonValueKind.True;

        if (element.TryGetProperty("$comment", out var comment))
            prop.Comment = comment.GetString();

        if (element.TryGetProperty("format", out var format))
            prop.Format = format.GetString();

        if (element.TryGetProperty("minimum", out var min) && min.TryGetDouble(out var minVal))
            prop.Minimum = minVal;

        if (element.TryGetProperty("maximum", out var max) && max.TryGetDouble(out var maxVal))
            prop.Maximum = maxVal;

        if (element.TryGetProperty("minLength", out var minLen) && minLen.TryGetInt32(out var minLenVal))
            prop.MinLength = minLenVal;

        if (element.TryGetProperty("maxLength", out var maxLen) && maxLen.TryGetInt32(out var maxLenVal))
            prop.MaxLength = maxLenVal;

        if (element.TryGetProperty("pattern", out var pattern))
            prop.Pattern = pattern.GetString();

        if (element.TryGetProperty("enum", out var enumArray) && enumArray.ValueKind == JsonValueKind.Array)
        {
            prop.EnumValues = [];
            foreach (var item in enumArray.EnumerateArray())
            {
                prop.EnumValues.Add(item.ToString());
            }
        }

        if (element.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            prop.ArrayItemSchema = ParseProperty(name + "_item", items);
        }

        if (prop.JsonType == "object" &&
            (element.TryGetProperty("properties", out _) ||
             element.TryGetProperty("additionalProperties", out _)))
        {
            prop.ObjectSchema = ParseObjectSchema(element);
        }

        prop.UiType = DetermineUiType(prop);
        return prop;
    }

    /// <summary>
    /// Reads "type" as a string or as an array (nullable union) and determines the
    /// effective non-null type. Does not throw for ValueKind Array.
    /// </summary>
    private static void ParseType(SchemaProperty prop, JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeProp))
            return;

        switch (typeProp.ValueKind)
        {
            case JsonValueKind.String:
                prop.JsonType = typeProp.GetString() ?? "string";
                break;

            case JsonValueKind.Array:
            {
                var types = new List<string>();
                bool nullable = false;
                foreach (var item in typeProp.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        continue;
                    var typeName = item.GetString();
                    if (typeName == "null")
                    {
                        nullable = true;
                        continue;
                    }
                    if (typeName is not null)
                        types.Add(typeName);
                }

                prop.IsNullable = nullable;
                prop.JsonTypes = types;
                if (types.Count > 0)
                    prop.JsonType = types[0];
                break;
            }

            default:
                // Unerwarteter ValueKind (z. B. Objekt) – bestehenden Default beibehalten.
                break;
        }
    }

    private static UiElementType DetermineUiType(SchemaProperty prop)
    {
        if (prop.EnumValues is { Count: > 0 })
            return UiElementType.ComboBox;

        return prop.JsonType switch
        {
            "string" when prop.Format == "date" => UiElementType.DatePicker,
            "string" when prop.Format == "date-time" => UiElementType.DatePicker,
            "string" when prop.Format == "time" => UiElementType.DatePicker,
            "string" when prop.Format == "email" => UiElementType.EmailBox,
            "string" when prop.Format == "uri" => UiElementType.UriBox,
            "string" when prop.Format == "password" => UiElementType.PasswordBox,
            "string" when prop.Format == "color" => UiElementType.ColorPicker,
            "string" when prop.Format == "file" => UiElementType.FilePicker,
            "string" => UiElementType.TextBox,
            "number" => UiElementType.NumberBox,
            "integer" => UiElementType.NumberBox,
            "boolean" => UiElementType.CheckBox,
            "object" => UiElementType.ObjectPanel,
            "array" => UiElementType.ArrayPanel,
            _ => UiElementType.TextBox,
        };
    }
}

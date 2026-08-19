using System.Text.Json;
using ConfixJson.Core.Models;

namespace ConfixJson.Core.Services;

public class SchemaParser
{
    public SchemaModel Parse(JsonDocument schema)
    {
        var root = schema.RootElement;
        var model = new SchemaModel();

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

    private static void ParseAdditionalProperties(SchemaModel model, JsonElement element)
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

    private static SchemaProperty ParseProperty(string name, JsonElement element)
    {
        var prop = new SchemaProperty { Name = name };

        if (element.TryGetProperty("type", out var typeProp))
        {
            prop.JsonType = typeProp.GetString() ?? "string";
        }

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

        if (prop.JsonType == "object" && element.TryGetProperty("properties", out _))
        {
            using var doc = JsonDocument.Parse(element.GetRawText());
            prop.ObjectSchema = new SchemaParser().Parse(doc);
        }

        prop.UiType = DetermineUiType(prop);
        return prop;
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

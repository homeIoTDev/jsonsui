using System.Text.Json;

namespace ConfixJson.Core.Services;

public class SchemaValidator
{
    public List<string> Validate(JsonDocument schema)
    {
        var errors = new List<string>();
        var root = schema.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add("Schema root must be a JSON object.");
            return errors;
        }

        if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "object")
        {
            errors.Add("Schema root must have 'type': 'object'.");
        }

        if (root.TryGetProperty("properties", out var props) && props.ValueKind != JsonValueKind.Object)
        {
            errors.Add("Schema 'properties' must be a JSON object.");
        }

        return errors;
    }

    public bool IsValidJsonSchema(JsonDocument schema)
    {
        return Validate(schema).Count == 0;
    }
}

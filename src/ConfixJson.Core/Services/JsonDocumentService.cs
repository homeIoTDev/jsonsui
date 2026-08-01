using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConfixJson.Core.Services;

public static class JsonDocumentService
{
    public static JsonNode? GetByPath(JsonNode root, string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(segment, out var val))
                current = val!;
            else if (current is JsonArray arr && int.TryParse(segment, out var idx) && idx >= 0 && idx < arr.Count)
                current = arr[idx]!;
            else
                return null;
        }
        return current;
    }

    public static JsonNode SetByPath(JsonNode root, string[] path, JsonNode value)
    {
        var cloned = Clone(root);
        if (path.Length == 0)
            return value.DeepClone();

        var parent = cloned;
        for (int i = 0; i < path.Length - 1; i++)
        {
            var seg = path[i];
            if (parent is JsonObject o && o.TryGetPropertyValue(seg, out var child))
                parent = child!;
            else if (parent is JsonArray a && int.TryParse(seg, out var idx) && idx >= 0 && idx < a.Count)
                parent = a[idx]!;
            else
                return cloned;
        }

        var lastSeg = path[^1];
        if (parent is JsonObject obj2)
            obj2[lastSeg] = value.DeepClone();
        else if (parent is JsonArray arr2 && int.TryParse(lastSeg, out var idx2) && idx2 >= 0 && idx2 < arr2.Count)
            arr2[idx2] = value.DeepClone();

        return cloned;
    }

    public static JsonNode RemoveArrayItem(JsonNode root, string[] arrayPath, int index)
    {
        var cloned = Clone(root);
        var arr = GetByPath(cloned, arrayPath) as JsonArray;
        if (arr != null && index >= 0 && index < arr.Count)
            arr.RemoveAt(index);
        return cloned;
    }

    public static JsonNode AddArrayItem(JsonNode root, string[] arrayPath, JsonNode template)
    {
        var cloned = Clone(root);
        var arr = GetByPath(cloned, arrayPath) as JsonArray;
        if (arr != null)
            arr.Add(template.DeepClone());
        return cloned;
    }

    public static JsonNode Clone(JsonNode node) => node.DeepClone();

    public static string ToFormattedJson(JsonNode node)
    {
        return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string GetScalarPreview(JsonNode? node, int maxLen = 40)
    {
        if (node is JsonValue val)
        {
            var s = val.ToJsonString();
            if (s.Length > maxLen)
                return s[..(maxLen - 3)] + "...";
            return s;
        }
        return "";
    }

    public static JsonNode? CreateTemplate(JsonNode? sample)
    {
        return sample switch
        {
            JsonObject obj =>
                new JsonObject(obj.Select(kv => KeyValuePair.Create(kv.Key, CreateTemplate(kv.Value)))),
            JsonArray arr =>
                new JsonArray(arr.Select(CreateTemplate).ToArray()),
            JsonValue v when v.TryGetValue<string>(out _) => JsonValue.Create(""),
            JsonValue v when v.TryGetValue<bool>(out _) => JsonValue.Create(false),
            JsonValue v when v.TryGetValue<int>(out _) => JsonValue.Create(0),
            JsonValue v when v.TryGetValue<long>(out _) => JsonValue.Create(0L),
            JsonValue v when v.TryGetValue<double>(out _) => JsonValue.Create(0.0),
            _ => JsonValue.Create<string?>(null)
        };
    }
}

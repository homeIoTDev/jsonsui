using System.Text.Json;
using System.Text.Json.Nodes;
using Jsonsui.Core.Models;

namespace Jsonsui.Core.Services;

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

    public static JsonNode SetByPath(JsonNode root, string[] path, JsonNode? value)
    {
        var cloned = Clone(root);
        if (path.Length == 0)
            return value?.DeepClone() ?? JsonValue.Create<string?>(null)!;

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
        var newValue = value?.DeepClone() ?? JsonValue.Create<string?>(null);
        if (parent is JsonObject obj2)
            obj2[lastSeg] = newValue;
        else if (parent is JsonArray arr2 && int.TryParse(lastSeg, out var idx2) && idx2 >= 0 && idx2 < arr2.Count)
            arr2[idx2] = newValue;

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

    public static AddPropertyResult AddProperty(JsonNode root, string[] objectPath, string name, JsonNode? value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return AddPropertyResult.Fail(PropertyAddFailure.InvalidPropertyName, root);

        var parent = GetByPath(root, objectPath);
        if (parent == null)
            return AddPropertyResult.Fail(PropertyAddFailure.ParentNotFound, root);
        if (parent is not JsonObject obj)
            return AddPropertyResult.Fail(PropertyAddFailure.ParentNotObject, root);
        if (obj.ContainsKey(name))
            return AddPropertyResult.Fail(PropertyAddFailure.PropertyAlreadyExists, root);

        var cloned = Clone(root);
        var clonedParent = GetByPath(cloned, objectPath) as JsonObject;
        clonedParent![name] = value?.DeepClone();
        return AddPropertyResult.Ok(cloned);
    }

    public static RemovePropertyResult RemoveProperty(JsonNode root, string[] objectPath, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RemovePropertyResult.Fail(RemovePropertyFailure.InvalidPropertyName, root);

        var parent = GetByPath(root, objectPath);
        if (parent == null)
            return RemovePropertyResult.Fail(RemovePropertyFailure.ParentNotFound, root);
        if (parent is not JsonObject obj)
            return RemovePropertyResult.Fail(RemovePropertyFailure.ParentNotObject, root);
        if (!obj.ContainsKey(name))
            return RemovePropertyResult.Fail(RemovePropertyFailure.PropertyNotFound, root);

        var cloned = Clone(root);
        var clonedParent = GetByPath(cloned, objectPath) as JsonObject;
        clonedParent!.Remove(name);
        return RemovePropertyResult.Ok(cloned);
    }

    public static RenamePropertyResult RenameProperty(JsonNode root, string[] objectPath, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return RenamePropertyResult.Fail(RenamePropertyFailure.InvalidPropertyName, root);

        var parent = GetByPath(root, objectPath);
        if (parent == null)
            return RenamePropertyResult.Fail(RenamePropertyFailure.ParentNotFound, root);
        if (parent is not JsonObject obj)
            return RenamePropertyResult.Fail(RenamePropertyFailure.ParentNotObject, root);
        if (!obj.ContainsKey(oldName))
            return RenamePropertyResult.Fail(RenamePropertyFailure.PropertyNotFound, root);
        if (obj.ContainsKey(newName))
            return RenamePropertyResult.Fail(RenamePropertyFailure.PropertyAlreadyExists, root);

        var cloned = Clone(root);
        var clonedParent = GetByPath(cloned, objectPath) as JsonObject;
        var value = clonedParent![oldName];
        clonedParent.Remove(oldName);
        clonedParent[newName] = value;
        return RenamePropertyResult.Ok(cloned);
    }

    public static JsonNode Clone(JsonNode node) => node.DeepClone();

    public static string ToFormattedJson(JsonNode node)
    {
        return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string GetScalarPreview(JsonNode? node, int maxLen = 40)
    {
        if (node is not JsonValue val)
            return "";

        var s = val.GetValueKind() switch
        {
            JsonValueKind.String => val.GetValue<string>() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => val.ToJsonString()
        };

        if (s.Length > maxLen)
            return s[..(maxLen - 3)] + "...";
        return s;
    }

    /// <summary>
    /// Converts freely entered text into a JSON value: valid JSON (number, boolean,
    /// object, array, quoted string, null) is used as-is, otherwise the text is
    /// treated as a JSON string.
    /// </summary>
    public static JsonNode? ParseFlexibleJsonValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return JsonValue.Create("");

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
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

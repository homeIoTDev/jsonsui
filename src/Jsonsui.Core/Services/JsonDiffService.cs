using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jsonsui.Core.Models;

namespace Jsonsui.Core.Services;

public class JsonDiffLine
{
    public char Marker { get; init; }
    public string Text { get; init; } = "";
    public string Type { get; init; } = "same"; // same, removed, added
}

public static class JsonDiffService
{
    // --- Legacy line-based diff (kept for the TUI) ---

    public static List<JsonDiffLine> ComputeDiff(JsonNode original, JsonNode current)
        => ComputeDiff(
            JsonDocumentService.ToFormattedJson(original),
            JsonDocumentService.ToFormattedJson(current));

    public static List<JsonDiffLine> ComputeDiff(string original, string current)
    {
        var lines = new List<JsonDiffLine>();
        var origFormatted = FormatJson(original);
        var currFormatted = FormatJson(current);

        var origLines = origFormatted.Split('\n');
        var currLines = currFormatted.Split('\n');

        int i = 0, j = 0;
        while (i < origLines.Length || j < currLines.Length)
        {
            if (i < origLines.Length && j < currLines.Length && origLines[i] == currLines[j])
            {
                lines.Add(new JsonDiffLine { Marker = ' ', Text = origLines[i], Type = "same" });
                i++; j++;
            }
            else if (i < origLines.Length && (j >= currLines.Length || !ContainsLine(currLines, origLines[i], j)))
            {
                lines.Add(new JsonDiffLine { Marker = '-', Text = origLines[i], Type = "removed" });
                i++;
            }
            else if (j < currLines.Length)
            {
                lines.Add(new JsonDiffLine { Marker = '+', Text = currLines[j], Type = "added" });
                j++;
            }
            else
            {
                break;
            }
        }

        return lines;
    }

    private static bool ContainsLine(string[] lines, string target, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            if (lines[i] == target) return true;
        }
        return false;
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    // --- Structural diff ---

    /// <summary>
    /// Compares two JSON trees structurally. Returns null when there is no change.
    /// </summary>
    public static JsonDiffNode? Compare(JsonNode? original, JsonNode? current)
    {
        if (JsonNode.DeepEquals(original, current))
            return null;

        return CompareNode(original, current, [], "");
    }

    private static JsonDiffNode? CompareNode(JsonNode? oldNode, JsonNode? newNode, string[] path, string displayPath)
    {
        if (JsonNode.DeepEquals(oldNode, newNode))
            return null;

        if (oldNode is JsonObject oldObj && newNode is JsonObject newObj)
        {
            var children = new List<JsonDiffNode>();
            var keys = new List<string>();
            foreach (var kv in oldObj) keys.Add(kv.Key);
            foreach (var kv in newObj)
                if (!oldObj.ContainsKey(kv.Key)) keys.Add(kv.Key);

            foreach (var key in keys)
            {
                var childPath = AppendSegment(path, key);
                var childDisplay = AppendKey(displayPath, key);
                var inOld = oldObj.ContainsKey(key);
                var inNew = newObj.ContainsKey(key);

                if (inOld && inNew)
                {
                    var child = CompareNode(oldObj[key], newObj[key], childPath, childDisplay);
                    if (child != null) children.Add(child);
                }
                else if (inOld)
                {
                    children.Add(Leaf(JsonChangeKind.Removed, childPath, childDisplay, Preview(oldObj[key]), null));
                }
                else
                {
                    children.Add(Leaf(JsonChangeKind.Added, childPath, childDisplay, null, Preview(newObj[key])));
                }
            }

            return children.Count == 0 ? null : Container(path, displayPath, children);
        }

        if (oldNode is JsonArray oldArr && newNode is JsonArray newArr)
        {
            var children = new List<JsonDiffNode>();
            var count = Math.Max(oldArr.Count, newArr.Count);
            for (var i = 0; i < count; i++)
            {
                var childPath = AppendSegment(path, i.ToString());
                var childDisplay = AppendIndex(displayPath, i);
                var inOld = i < oldArr.Count;
                var inNew = i < newArr.Count;

                if (inOld && inNew)
                {
                    var child = CompareNode(oldArr[i], newArr[i], childPath, childDisplay);
                    if (child != null) children.Add(child);
                }
                else if (inOld)
                {
                    children.Add(Leaf(JsonChangeKind.Removed, childPath, childDisplay, Preview(oldArr[i]), null));
                }
                else
                {
                    children.Add(Leaf(JsonChangeKind.Added, childPath, childDisplay, null, Preview(newArr[i])));
                }
            }

            return children.Count == 0 ? null : Container(path, displayPath, children);
        }

        // Scalar change or type mismatch (also handles different root types).
        return new JsonDiffNode
        {
            PathSegments = path,
            DisplayPath = displayPath,
            Kind = JsonChangeKind.Modified,
            OldValue = Preview(oldNode),
            NewValue = Preview(newNode),
            IsContainer = false,
            ModifiedCount = 1
        };
    }

    private static JsonDiffNode Container(string[] path, string displayPath, List<JsonDiffNode> children)
    {
        var added = 0;
        var removed = 0;
        var modified = 0;
        foreach (var child in children)
        {
            added += child.AddedCount;
            removed += child.RemovedCount;
            modified += child.ModifiedCount;
        }

        return new JsonDiffNode
        {
            PathSegments = path,
            DisplayPath = displayPath,
            Kind = JsonChangeKind.Unchanged,
            IsContainer = true,
            Children = children,
            AddedCount = added,
            RemovedCount = removed,
            ModifiedCount = modified
        };
    }

    private static JsonDiffNode Leaf(JsonChangeKind kind, string[] path, string displayPath, string? oldValue, string? newValue)
    {
        return new JsonDiffNode
        {
            PathSegments = path,
            DisplayPath = displayPath,
            Kind = kind,
            OldValue = oldValue,
            NewValue = newValue,
            IsContainer = false,
            AddedCount = kind == JsonChangeKind.Added ? 1 : 0,
            RemovedCount = kind == JsonChangeKind.Removed ? 1 : 0,
            ModifiedCount = kind == JsonChangeKind.Modified ? 1 : 0
        };
    }

    private static string[] AppendSegment(string[] path, string segment)
    {
        var result = new string[path.Length + 1];
        Array.Copy(path, result, path.Length);
        result[path.Length] = segment;
        return result;
    }

    private static string AppendKey(string displayPath, string key)
        => displayPath.Length == 0 ? key : displayPath + "." + key;

    private static string AppendIndex(string displayPath, int index)
        => displayPath + "[" + index + "]";

    private static string Preview(JsonNode? node)
    {
        return node switch
        {
            null => "null",
            JsonValue value => JsonDocumentService.GetScalarPreview(value, 80),
            JsonObject => "{…}",
            JsonArray array => $"[{array.Count}]",
            _ => ""
        };
    }
}

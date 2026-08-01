using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace ConfixJson.Core.Models;

public class JsonEditorNode
{
    public JsonEditorNode? Parent { get; set; }
    public string Label { get; set; } = "";
    public string[] Path { get; set; } = [];
    public string PathKey => string.Join("/", Path);
    public JsonNode? Value { get; set; }
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool HasErrors { get; set; }
    public List<JsonEditorNode> Children { get; set; } = [];
    public int Depth { get; set; }
    public string NodeType { get; set; } = "scalar";

    public bool IsExpandable => NodeType == "root" || NodeType == "object";

    public string TypeTag =>
        NodeType switch
        {
            "object" => $"{{{Children.Count} fields}}",
            "array" => $"[{((JsonArray?)Value)?.Count ?? 0} items]",
            _ => Services.JsonDocumentService.GetScalarPreview(Value)
        };

    public string Icon =>
        NodeType switch
        {
            "object" => IsExpanded ? "chevron-open" : "chevron",
            "root" => IsExpanded ? "chevron-open" : "chevron",
            "array" => "bracket",
            _ => "dot"
        };
}

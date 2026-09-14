using System.Text.Json.Nodes;

namespace Jsonsui.Core.Models;

public class EditorState
{
    public JsonNode Json { get; set; } = new JsonObject();
    public JsonNode OriginalJson { get; set; } = new JsonObject();
    public string[] SelectedPath { get; set; } = [];
    public HashSet<string> Expanded { get; set; } = [];
    public int? CardIndex { get; set; }
    public NestedContext? NestedCtx { get; set; }
    public SchemaModel? ActiveSchema { get; set; }
    public SchemaLoadStatus SchemaStatus { get; set; } = SchemaLoadStatus.None;
    public string? SchemaFilePath { get; set; }
    public bool SchemaAutoDetected { get; set; }
    public ValidationError[] Errors { get; set; } = [];
    public string[]? FocusFieldPath { get; set; }
}

using System.Text.Json.Nodes;

namespace ConfixJson.Core.Models;

public class EditorState
{
    public JsonNode Json { get; set; } = new JsonObject();
    public JsonNode OriginalJson { get; set; } = new JsonObject();
    public string[] SelectedPath { get; set; } = [];
    public HashSet<string> Expanded { get; set; } = [];
    public int? CardIndex { get; set; }
    public NestedContext? NestedCtx { get; set; }
    public SchemaModel? ActiveSchema { get; set; }
    public ValidationError[] Errors { get; set; } = [];
    public string[]? FocusFieldPath { get; set; }
}

using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConfixJson.UI.Models;

public sealed partial class JsonEditorNode : ObservableObject
{
    public JsonEditorNode? Parent { get; set; }

    [ObservableProperty]
    public partial string Label { get; set; } = "";

    public string[] Path { get; set; } = [];
    public string PathKey => string.Join("/", Path);

    public JsonNode? Value { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool HasErrors { get; set; }

    public ObservableCollection<JsonEditorNode> Children { get; } = [];

    public int Depth { get; set; }
    public string NodeType { get; set; } = "scalar"; // root, object, array, scalar

    public bool IsExpandable => NodeType == "root" || NodeType == "object";

    public string TypeTag
    {
        get
        {
            return NodeType switch
            {
                "object" => $"{{{Children.Count} fields}}",
                "array" => $"[{((JsonArray?)Value)?.Count ?? 0} items]",
                _ => Core.Services.JsonDocumentService.GetScalarPreview(Value)
            };
        }
    }

    public string Icon => NodeType switch
    {
        "object" => "chevron",
        "root" => "chevron-open",
        "array" => "bracket",
        _ => "dot"
    };

    public string StyleClass => NodeType switch
    {
        "root" => "accent-bold",
        "object" => "bold",
        "array" => "accent-color",
        _ => "normal"
    };

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Icon));
    }
}

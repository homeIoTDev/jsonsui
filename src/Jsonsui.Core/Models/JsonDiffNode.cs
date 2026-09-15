namespace Jsonsui.Core.Models;

public sealed class JsonDiffNode
{
    public string[] PathSegments { get; init; } = [];
    public string DisplayPath { get; init; } = "";
    public JsonChangeKind Kind { get; init; } = JsonChangeKind.Unchanged;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public bool IsContainer { get; init; }
    public List<JsonDiffNode> Children { get; init; } = [];
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ModifiedCount { get; init; }

    public int ChangeCount => AddedCount + RemovedCount + ModifiedCount;
    public bool HasChanges => ChangeCount > 0;
}

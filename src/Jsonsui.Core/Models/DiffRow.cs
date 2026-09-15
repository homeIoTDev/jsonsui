namespace Jsonsui.Core.Models;

public sealed class DiffRow
{
    public JsonChangeKind Kind { get; init; }
    public string DisplayPath { get; init; } = "";
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }

    public bool IsAdded => Kind == JsonChangeKind.Added;
    public bool IsRemoved => Kind == JsonChangeKind.Removed;
    public bool IsModified => Kind == JsonChangeKind.Modified;

    public string ValueText => Kind switch
    {
        JsonChangeKind.Modified => $"{OldValue} → {NewValue}",
        JsonChangeKind.Added => NewValue ?? "null",
        JsonChangeKind.Removed => OldValue ?? "null",
        _ => ""
    };
}

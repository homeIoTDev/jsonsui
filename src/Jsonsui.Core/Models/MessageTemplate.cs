namespace Jsonsui.Core.Models;

/// <summary>
/// UI-neutral message key with arguments. The concrete language is resolved
/// later in the UI via the resource files.
/// </summary>
public sealed class MessageTemplate
{
    public string Key { get; init; } = "";
    public object?[] Args { get; init; } = [];
}

namespace Jsonsui.Core.Models;

/// <summary>
/// UI-neutraler Nachrichtenschlüssel samt Argumenten. Die konkrete Sprache
/// wird erst in der UI über die Ressourcen aufgelöst.
/// </summary>
public sealed class MessageTemplate
{
    public string Key { get; init; } = "";
    public object?[] Args { get; init; } = [];
}

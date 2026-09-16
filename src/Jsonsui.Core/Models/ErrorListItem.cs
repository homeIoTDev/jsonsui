namespace Jsonsui.Core.Models;

public class ErrorListItem
{
    public string[] Path { get; set; } = [];
    public string DisplayPath { get; set; } = "";
    public MessageTemplate Message { get; set; } = new();
    public bool IsMissing { get; set; }
}

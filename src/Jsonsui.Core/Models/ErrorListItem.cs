namespace Jsonsui.Core.Models;

public class ErrorListItem
{
    public string[] Path { get; set; } = [];
    public string DisplayPath { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsMissing { get; set; }
}

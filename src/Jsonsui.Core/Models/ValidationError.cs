namespace Jsonsui.Core.Models;

public class ValidationError
{
    public string[] Path { get; set; } = [];
    public MessageTemplate Message { get; set; } = new();
    public string PathString => string.Join(".", Path);
}

namespace Jsonsui.Core.Models;

public class ValidationError
{
    public string[] Path { get; set; } = [];
    public string Message { get; set; } = "";
    public string PathString => string.Join(".", Path);
}

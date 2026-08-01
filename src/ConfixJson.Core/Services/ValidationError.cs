namespace ConfixJson.Core.Services;

public class ValidationError
{
    public string[] Path { get; init; } = [];
    public string Message { get; init; } = "";
    public string PathString => string.Join(".", Path);
}

namespace ConfixJson.Core.Models;

public class SchemaModel
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? SchemaUri { get; set; }
    public List<SchemaProperty> Properties { get; set; } = [];
    public List<string> Required { get; set; } = [];
}

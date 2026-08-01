namespace ConfixJson.UI.Models;

public class CardItem
{
    public int Index { get; set; }
    public string[] ItemPath { get; init; } = [];
    public bool HasErrors { get; set; }
    public string Preview { get; set; } = "";
    public bool IsSelected { get; set; }
}

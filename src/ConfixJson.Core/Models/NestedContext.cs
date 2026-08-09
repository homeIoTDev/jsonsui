namespace ConfixJson.Core.Models;

public class NestedContext
{
    public string[]? ArrayPath { get; set; }
    public int? CardIndex { get; set; }
    public string[]? ObjectPath { get; set; }
    public NestedContext? Previous { get; set; }
}

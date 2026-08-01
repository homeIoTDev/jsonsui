using System.Text;
using System.Text.Json;

namespace ConfixJson.Core.Services;

public class JsonDiffLine
{
    public char Marker { get; init; }
    public string Text { get; init; } = "";
    public string Type { get; init; } = "same"; // same, removed, added
}

public static class JsonDiffService
{
    public static List<JsonDiffLine> ComputeDiff(string original, string current)
    {
        var lines = new List<JsonDiffLine>();
        var origFormatted = FormatJson(original);
        var currFormatted = FormatJson(current);

        var origLines = origFormatted.Split('\n');
        var currLines = currFormatted.Split('\n');

        int i = 0, j = 0;
        while (i < origLines.Length || j < currLines.Length)
        {
            if (i < origLines.Length && j < currLines.Length && origLines[i] == currLines[j])
            {
                lines.Add(new JsonDiffLine { Marker = ' ', Text = origLines[i], Type = "same" });
                i++; j++;
            }
            else if (i < origLines.Length && (j >= currLines.Length || !ContainsLine(currLines, origLines[i], j)))
            {
                lines.Add(new JsonDiffLine { Marker = '-', Text = origLines[i], Type = "removed" });
                i++;
            }
            else if (j < currLines.Length)
            {
                lines.Add(new JsonDiffLine { Marker = '+', Text = currLines[j], Type = "added" });
                j++;
            }
            else
            {
                break;
            }
        }

        return lines;
    }

    private static bool ContainsLine(string[] lines, string target, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            if (lines[i] == target) return true;
        }
        return false;
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}

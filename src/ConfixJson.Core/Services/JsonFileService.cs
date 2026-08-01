using System.Text.Json.Nodes;

namespace ConfixJson.Core.Services;

public static class JsonFileService
{
    public static JsonNode LoadFromFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return JsonNode.Parse(content)
            ?? throw new InvalidOperationException($"Failed to parse JSON: {filePath}");
    }

    public static JsonNode LoadFromStream(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        return JsonNode.Parse(content)
            ?? throw new InvalidOperationException("Failed to parse JSON from stream");
    }

    public static async Task<JsonNode> LoadFromStreamAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return JsonNode.Parse(content)
            ?? throw new InvalidOperationException("Failed to parse JSON from stream");
    }

    public static void SaveToFile(string filePath, JsonNode document)
    {
        var json = JsonDocumentService.ToFormattedJson(document);
        File.WriteAllText(filePath, json);
    }
}

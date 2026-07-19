using System.Text.Json;

namespace ConfixJson.Core.Services;

public class SchemaLoader
{
    public async Task<JsonDocument> LoadFromFileAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonDocument.ParseAsync(stream);
    }

    public JsonDocument LoadFromString(string json)
    {
        return JsonDocument.Parse(json);
    }
}

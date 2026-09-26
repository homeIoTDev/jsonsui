using System.Text.Json.Nodes;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;

namespace Jsonsui.Core.Tests.TestSupport;

internal static class EditorStateFactory
{
    public static EditorState FromFiles(string jsonFile, string? schemaFile = null)
    {
        var schema = schemaFile == null ? null : LoadSchema(schemaFile);
        var json = EditorLogic.LoadDocument(TestDataPaths.File(jsonFile));
        return FromJson(json, schema);
    }

    public static EditorState FromJsonString(string json, SchemaModel? schema = null)
        => FromJson(JsonNode.Parse(json)!, schema);

    public static EditorState FromJson(JsonNode json, SchemaModel? schema = null)
    {
        var state = new EditorState { ActiveSchema = schema };
        EditorLogic.InitDocument(state, json, "test.json");
        return state;
    }

    public static SchemaModel LoadSchema(string schemaFile)
    {
        var document = new SchemaLoader().LoadFromString(File.ReadAllText(TestDataPaths.File(schemaFile)));
        return new SchemaParser().Parse(document);
    }

    public static void Select(EditorState state, params string[] path)
    {
        var root = EditorLogic.BuildTree(state, null);
        var node = FindNode(root, path)
            ?? throw new InvalidOperationException($"Tree node not found: {string.Join("/", path)}");
        EditorLogic.SelectNode(state, node);
    }

    public static JsonEditorNode? FindNode(JsonEditorNode node, string[] path)
    {
        if (node.Path.SequenceEqual(path)) return node;
        foreach (var child in node.Children)
        {
            var found = FindNode(child, path);
            if (found != null) return found;
        }
        return null;
    }
}

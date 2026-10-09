using System.Text.Json;
using System.Text.Json.Nodes;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class AddArrayItemTests
{
    private static JsonNode Item(EditorState state, string array, int index)
        => state.Json[array]![index]!;

    private static void Add(EditorState state, string array)
        => EditorLogic.AddArrayItem(state, new[] { array }, new UndoRedoService());

    [Fact]
    public void AddItem_ToSchemaObjectArray_CreatesEmptyObjectWithoutCloningFirstItem()
    {
        var state = EditorStateFactory.FromFiles("test_example.json", "test_example.schema.json");
        EditorStateFactory.Select(state, "servers");

        var firstBefore = (JsonNode)state.Json["servers"]![0]!.DeepClone();
        var countBefore = ((JsonArray)state.Json["servers"]!).Count;

        Add(state, "servers");

        var arr = (JsonArray)state.Json["servers"]!;
        Assert.Equal(countBefore + 1, arr.Count);
        Assert.Empty(Assert.IsType<JsonObject>(arr[countBefore]));
        Assert.True(JsonNode.DeepEquals(firstBefore, arr[0]));
    }

    [Fact]
    public void AddItem_ToSchemaArrayOfArrays_CreatesEmptyArray()
    {
        var state = EditorStateFactory.FromFiles("test_example.json", "test_example.schema.json");
        EditorStateFactory.Select(state, "matrixData");

        var firstBefore = (JsonNode)state.Json["matrixData"]![0]!.DeepClone();

        Add(state, "matrixData");

        var arr = (JsonArray)state.Json["matrixData"]!;
        Assert.Equal(3, arr.Count);
        Assert.Empty(Assert.IsType<JsonArray>(arr[2]));
        Assert.True(JsonNode.DeepEquals(firstBefore, arr[0]));
    }

    [Fact]
    public void AddItem_ToEmptySchemaObjectArray_CreatesEmptyObject()
    {
        var state = EditorStateFactory.FromFiles("test_example.json", "test_example.schema.json");
        EditorStateFactory.Select(state, "workflows");

        Add(state, "workflows");

        var arr = (JsonArray)state.Json["workflows"]!;
        Assert.Single(arr);
        Assert.Empty(Assert.IsType<JsonObject>(arr[0]));
    }

    [Fact]
    public void AddItem_ToSchemalessObjectArray_CreatesEmptyObjectWithoutCloningKeys()
    {
        var state = EditorStateFactory.FromJsonString("{\"o\":[{\"x\":1,\"y\":2}]}");
        var firstBefore = (JsonNode)state.Json["o"]![0]!.DeepClone();

        Add(state, "o");

        var arr = (JsonArray)state.Json["o"]!;
        Assert.Equal(2, arr.Count);
        Assert.Empty(Assert.IsType<JsonObject>(arr[1]));
        Assert.True(JsonNode.DeepEquals(firstBefore, arr[0]));
    }

    [Fact]
    public void AddItem_ToSchemalessArrayOfArrays_CreatesEmptyArray()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[[1,2]]}");
        var firstBefore = (JsonNode)state.Json["a"]![0]!.DeepClone();

        Add(state, "a");

        var arr = (JsonArray)state.Json["a"]!;
        Assert.Equal(2, arr.Count);
        Assert.Empty(Assert.IsType<JsonArray>(arr[1]));
        Assert.True(JsonNode.DeepEquals(firstBefore, arr[0]));
    }

    [Fact]
    public void AddItem_ToSchemalessStringArray_CreatesEmptyString()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[\"x\"]}");

        Add(state, "a");

        Assert.Equal("", Item(state, "a", 1).GetValue<string>());
    }

    [Fact]
    public void AddItem_ToSchemalessNumberArray_CreatesZero()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[1,2]}");

        Add(state, "a");

        Assert.Equal(0, Item(state, "a", 2).GetValue<int>());
    }

    [Fact]
    public void AddItem_ToSchemalessBooleanArray_CreatesFalse()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[true]}");

        Add(state, "a");

        Assert.False(Item(state, "a", 1).GetValue<bool>());
    }

    [Fact]
    public void AddItem_ToSchemalessNullItem_PreservesNull()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[null]}");

        Add(state, "a");

        var added = state.Json["a"]![1];
        Assert.True(added is null || added.GetValueKind() == JsonValueKind.Null);
    }

    [Fact]
    public void AddItem_ToEmptySchemalessArray_KeepsEmptyObjectFallback()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[]}");

        Add(state, "a");

        var arr = (JsonArray)state.Json["a"]!;
        Assert.Single(arr);
        Assert.Empty(Assert.IsType<JsonObject>(arr[0]));
    }
}

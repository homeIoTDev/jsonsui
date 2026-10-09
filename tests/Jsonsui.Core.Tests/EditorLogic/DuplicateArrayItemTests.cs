using System.Text.Json;
using System.Text.Json.Nodes;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class DuplicateArrayItemTests
{
    private static JsonArray ArrayOf(EditorState state, string array) => (JsonArray)state.Json[array]!;

    [Fact]
    public void Duplicate_ObjectItemWithMultipleProperties_CopiesAllValues()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":1,\"y\":\"two\",\"z\":true}]}");
        var original = (JsonNode)state.Json["a"]![0]!.DeepClone();

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        var arr = ArrayOf(state, "a");
        Assert.Equal(2, arr.Count);
        Assert.True(JsonNode.DeepEquals(original, arr[1]));
    }

    [Fact]
    public void Duplicate_NestedObjectsAndArrays_CopiesValuesDeeply()
    {
        var state = EditorStateFactory.FromJsonString(
            "{\"a\":[{\"nested\":{\"k\":[1,2,3]},\"list\":[{\"p\":1},{\"q\":\"v\"}]}]}");

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        var arr = ArrayOf(state, "a");
        Assert.Equal(2, arr.Count);
        Assert.True(JsonNode.DeepEquals(arr[0], arr[1]));

        var copy = (JsonObject)arr[1]!;
        Assert.Equal(3, ((JsonArray)copy["nested"]!["k"]!).Count);
        Assert.Equal(1, ((JsonObject)((JsonArray)copy["list"]!)[0]!)["p"]!.GetValue<int>());
        Assert.Equal("v", ((JsonObject)((JsonArray)copy["list"]!)[1]!)["q"]!.GetValue<string>());
    }

    [Fact]
    public void Duplicate_ItemWithExplicitNullProperty_CopiesNull()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":null,\"y\":1}]}");

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        var arr = ArrayOf(state, "a");
        Assert.True(JsonNode.DeepEquals(arr[0], arr[1]));
        var copy = (JsonObject)arr[1]!;
        Assert.True(copy["x"] is null || copy["x"]!.GetValueKind() == JsonValueKind.Null);
        Assert.Equal(1, copy["y"]!.GetValue<int>());
    }

    [Fact]
    public void Duplicate_NullArrayItem_PreservesNullItem()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[null,1]}");

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        var arr = ArrayOf(state, "a");
        Assert.Equal(3, arr.Count);
        Assert.True(arr[0] is null || arr[0]!.GetValueKind() == JsonValueKind.Null);
        Assert.True(arr[1] is null || arr[1]!.GetValueKind() == JsonValueKind.Null);
        Assert.Equal(1, arr[2]!.GetValue<int>());
    }

    [Fact]
    public void Duplicate_LeavesOriginalUnchanged()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":1,\"y\":2}]}");
        var original = (JsonNode)state.Json["a"]![0]!.DeepClone();

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        Assert.True(JsonNode.DeepEquals(original, state.Json["a"]![0]));
    }

    [Fact]
    public void Duplicate_CopyIsIndependentOfOriginal()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":1,\"nested\":{\"y\":2}}]}");

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        var arr = ArrayOf(state, "a");
        var original = (JsonObject)arr[0]!;
        var copy = (JsonObject)arr[1]!;
        Assert.NotSame(original, copy);

        original["x"] = 99;
        ((JsonObject)original["nested"]!)["y"] = 77;

        Assert.Equal(1, copy["x"]!.GetValue<int>());
        Assert.Equal(2, ((JsonObject)copy["nested"]!)["y"]!.GetValue<int>());
    }

    [Fact]
    public void Duplicate_DuplicatesGivenIndexEvenWhenAnotherItemIsSelected()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":1},{\"x\":2},{\"x\":3}]}");
        state.CardIndex = 2;

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        var arr = ArrayOf(state, "a");
        Assert.Equal(4, arr.Count);
        Assert.Equal(1, ((JsonObject)arr[0]!)["x"]!.GetValue<int>());
        Assert.Equal(1, ((JsonObject)arr[1]!)["x"]!.GetValue<int>());
        Assert.Equal(2, ((JsonObject)arr[2]!)["x"]!.GetValue<int>());
        Assert.Equal(3, ((JsonObject)arr[3]!)["x"]!.GetValue<int>());
    }

    [Fact]
    public void Duplicate_InsertsNextToOriginalAndSelectsCopy()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":1},{\"x\":2}]}");

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, new UndoRedoService());

        var arr = ArrayOf(state, "a");
        Assert.Equal(3, arr.Count);
        Assert.Equal(1, ((JsonObject)arr[1]!)["x"]!.GetValue<int>());
        Assert.Equal(2, ((JsonObject)arr[2]!)["x"]!.GetValue<int>());
        Assert.Equal(1, state.CardIndex);
    }

    [Fact]
    public void Duplicate_InsideNestedArray_InsertsAfterSourceAndUpdatesContext()
    {
        var state = EditorStateFactory.FromJsonString("{\"outer\":[[{\"x\":1}],[{\"x\":2}]]}");
        EditorLogic.DrillIntoArray(state, new[] { "outer" });

        EditorLogic.DuplicateArrayItem(state, new[] { "outer" }, 0, new UndoRedoService());

        var outer = (JsonArray)state.Json["outer"]!;
        Assert.Equal(3, outer.Count);
        Assert.True(JsonNode.DeepEquals(outer[0], outer[1]));

        var (path, index) = EditorLogic.GetCardArrayContext(state);
        Assert.Equal(new[] { "outer" }, path);
        Assert.Equal(1, index);
    }

    [Fact]
    public void Duplicate_UndoRemovesCopy_RedoReinsertsAtSamePosition()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":1},{\"x\":2}]}");
        var undoRedo = new UndoRedoService();

        EditorLogic.DuplicateArrayItem(state, new[] { "a" }, 0, undoRedo);
        Assert.Equal(3, ArrayOf(state, "a").Count);

        EditorLogic.Undo(state, undoRedo);
        Assert.Equal(2, ArrayOf(state, "a").Count);

        EditorLogic.Redo(state, undoRedo);
        var arr = ArrayOf(state, "a");
        Assert.Equal(3, arr.Count);
        Assert.Equal(1, ((JsonObject)arr[0]!)["x"]!.GetValue<int>());
        Assert.Equal(1, ((JsonObject)arr[1]!)["x"]!.GetValue<int>());
        Assert.Equal(2, ((JsonObject)arr[2]!)["x"]!.GetValue<int>());
    }
}

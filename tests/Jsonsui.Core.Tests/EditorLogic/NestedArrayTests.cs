using System.Text.Json.Nodes;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class NestedArrayTests
{
    [Fact]
    public void DeeplyNestedArrays_DrillDownThroughThreeLevelsToObject()
    {
        var state = EditorStateFactory.FromJsonString("{\"m\":[[[{\"v\":1}]]]}");
        state.SelectedPath = new[] { "m" };
        state.CardIndex = 0;

        var (level1Path, _) = EditorLogic.GetCardArrayContext(state);
        Assert.Equal(new[] { "m" }, level1Path);
        var level1Cards = EditorLogic.BuildCardItems(state, level1Path);
        Assert.Single(level1Cards);
        Assert.Equal("[1]", level1Cards[0].Preview);
        var level1Detail = EditorLogic.GetDetailItemPath(state)!;
        Assert.IsType<JsonArray>(EditorLogic.GetDetailItemValue(state));
        Assert.Equal("array", EditorLogic.BuildFieldRow(state, level1Detail).FieldType);

        EditorLogic.DrillIntoArray(state, level1Detail);
        var (level2Path, _) = EditorLogic.GetCardArrayContext(state);
        Assert.Equal(new[] { "m", "0" }, level2Path);
        var level2Cards = EditorLogic.BuildCardItems(state, level2Path);
        Assert.Single(level2Cards);
        Assert.Equal("[1]", level2Cards[0].Preview);
        var level2Detail = EditorLogic.GetDetailItemPath(state)!;
        Assert.IsType<JsonArray>(EditorLogic.GetDetailItemValue(state));

        EditorLogic.DrillIntoArray(state, level2Detail);
        var (level3Path, _) = EditorLogic.GetCardArrayContext(state);
        Assert.Equal(new[] { "m", "0", "0" }, level3Path);
        var level3Cards = EditorLogic.BuildCardItems(state, level3Path);
        Assert.Single(level3Cards);
        Assert.Equal("v: 1", level3Cards[0].Preview);
        var level3Detail = EditorLogic.GetDetailItemPath(state)!;
        Assert.Single(EditorLogic.BuildObjectFields(state, level3Detail));

        EditorLogic.ExitNestedArray(state);
        EditorLogic.ExitNestedArray(state);
        Assert.Equal(new[] { "m" }, EditorLogic.GetCardArrayContext(state).CardArrayPath);
    }
}

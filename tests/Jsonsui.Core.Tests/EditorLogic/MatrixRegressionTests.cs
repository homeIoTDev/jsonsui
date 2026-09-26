using System.Text.Json.Nodes;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class MatrixRegressionTests
{
    private static EditorState MatrixState()
        => EditorStateFactory.FromFiles("test_example.json", "test_example.schema.json");

    [Fact]
    public void MatrixData_Selected_ResolvesOuterArrayContext()
    {
        var state = MatrixState();
        EditorStateFactory.Select(state, "matrixData");

        Assert.Equal(new[] { "matrixData" }, state.SelectedPath);
        Assert.NotNull(state.CardIndex);
        Assert.Equal(0, state.CardIndex!.Value);

        var (cardArrayPath, activeIndex) = EditorLogic.GetCardArrayContext(state);
        Assert.Equal(new[] { "matrixData" }, cardArrayPath);
        Assert.NotNull(activeIndex);
        Assert.Equal(0, activeIndex!.Value);
    }

    [Fact]
    public void MatrixData_OuterCards_ShowNestedArrayPreview()
    {
        var state = MatrixState();
        EditorStateFactory.Select(state, "matrixData");

        var (cardArrayPath, _) = EditorLogic.GetCardArrayContext(state);
        var cards = EditorLogic.BuildCardItems(state, cardArrayPath);

        Assert.Equal(2, cards.Count);
        Assert.Equal("[2]", cards[0].Preview);
        Assert.Equal("[2]", cards[1].Preview);
    }

    [Fact]
    public void MatrixCard_DetailValue_IsJsonArray()
    {
        var state = MatrixState();
        EditorStateFactory.Select(state, "matrixData");

        Assert.Equal(new[] { "matrixData", "0" }, EditorLogic.GetDetailItemPath(state));
        Assert.IsType<JsonArray>(EditorLogic.GetDetailItemValue(state));
    }

    [Fact]
    public void MatrixCard_NestedArray_BuildFieldRowIsArrayWithItemCount()
    {
        var state = MatrixState();
        EditorStateFactory.Select(state, "matrixData");
        var detailPath = EditorLogic.GetDetailItemPath(state)!;

        var row = EditorLogic.BuildFieldRow(state, detailPath);

        Assert.Equal("array", row.FieldType);
        Assert.Equal(2, row.ArrayItemCount);
    }

    [Fact]
    public void MatrixCard_WithNestedArray_ShowsArrayDetailAndSupportsDrilldown()
    {
        var state = MatrixState();
        EditorStateFactory.Select(state, "matrixData");

        var (outerPath, outerIndex) = EditorLogic.GetCardArrayContext(state);
        var outerCards = EditorLogic.BuildCardItems(state, outerPath);
        Assert.Equal(2, outerCards.Count);
        Assert.NotNull(outerIndex);
        Assert.Equal(0, outerIndex!.Value);

        var detailPath = EditorLogic.GetDetailItemPath(state);
        Assert.Equal(new[] { "matrixData", "0" }, detailPath);
        Assert.IsType<JsonArray>(EditorLogic.GetDetailItemValue(state));

        var detailRow = EditorLogic.BuildFieldRow(state, detailPath!);
        Assert.Equal("array", detailRow.FieldType);
        Assert.Equal(2, detailRow.ArrayItemCount);

        EditorLogic.DrillIntoArray(state, detailPath!);
        var (innerPath, innerIndex) = EditorLogic.GetCardArrayContext(state);
        Assert.Equal(new[] { "matrixData", "0" }, innerPath);
        Assert.NotNull(innerIndex);
        Assert.Equal(0, innerIndex!.Value);
        Assert.Equal(new[] { "matrixData" }, state.SelectedPath);

        var innerCards = EditorLogic.BuildCardItems(state, innerPath);
        Assert.Equal(2, innerCards.Count);
        Assert.Equal("x: 1, y: 2, value: A", innerCards[0].Preview);
        Assert.Equal("x: 3, y: 4, value: B", innerCards[1].Preview);

        var objectPath = EditorLogic.GetDetailItemPath(state);
        Assert.Equal(new[] { "matrixData", "0", "0" }, objectPath);
        Assert.IsType<JsonObject>(EditorLogic.GetDetailItemValue(state));
        Assert.Equal(
            new[] { "x", "y", "value" },
            EditorLogic.BuildObjectFields(state, objectPath!).Select(f => f.Key));

        EditorLogic.ExitNestedArray(state);
        Assert.Null(state.NestedCtx);
        Assert.Equal(new[] { "matrixData" }, state.SelectedPath);
    }

    [Fact]
    public void MatrixCard_SelectingInnerCard_UpdatesDetailPath()
    {
        var state = MatrixState();
        EditorStateFactory.Select(state, "matrixData");
        var detailPath = EditorLogic.GetDetailItemPath(state)!;
        EditorLogic.DrillIntoArray(state, detailPath);

        EditorLogic.SelectCard(state, 1);

        Assert.Equal(new[] { "matrixData", "0", "1" }, EditorLogic.GetDetailItemPath(state));
        var value = Assert.IsType<JsonObject>(EditorLogic.GetDetailItemValue(state));
        Assert.Equal("B", value["value"]?.GetValue<string>());
    }
}

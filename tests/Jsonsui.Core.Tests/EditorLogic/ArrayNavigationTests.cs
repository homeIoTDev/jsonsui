using System.Text.Json.Nodes;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class ArrayNavigationTests
{
    private static EditorState ExampleState()
        => EditorStateFactory.FromFiles("test_example.json", "test_example.schema.json");

    [Fact]
    public void ObjectArray_Selected_ResolvesCardsAndDetailFields()
    {
        var state = ExampleState();
        EditorStateFactory.Select(state, "servers");

        Assert.Equal(new[] { "servers" }, state.SelectedPath);

        var (cardArrayPath, activeIndex) = EditorLogic.GetCardArrayContext(state);
        Assert.Equal(new[] { "servers" }, cardArrayPath);
        Assert.NotNull(activeIndex);
        Assert.Equal(0, activeIndex!.Value);

        var detailPath = EditorLogic.GetDetailItemPath(state)!;
        Assert.IsType<JsonObject>(EditorLogic.GetDetailItemValue(state));

        var fields = EditorLogic.BuildObjectFields(state, detailPath);
        Assert.True(fields.Count >= 5);
        Assert.Contains(fields, f => f.Key == "name");
        Assert.Contains(fields, f => f.Key == "host");

        var cards = EditorLogic.BuildCardItems(state, cardArrayPath);
        Assert.StartsWith("name: primary", cards[0].Preview);
    }
}

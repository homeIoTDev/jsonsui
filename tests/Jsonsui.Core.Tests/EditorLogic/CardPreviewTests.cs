using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class CardPreviewTests
{
    [Fact]
    public void ScalarItems_Preview_ShowsRawValues()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[1,\"two\",true]}");
        var cards = EditorLogic.BuildCardItems(state, new[] { "a" });

        Assert.Equal(3, cards.Count);
        Assert.Equal("1", cards[0].Preview);
        Assert.Equal("two", cards[1].Preview);
        Assert.Equal("true", cards[2].Preview);
    }

    [Fact]
    public void ObjectItems_Preview_SummarizesFirstFields()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[{\"x\":1,\"y\":2,\"value\":\"A\"}]}");
        var cards = EditorLogic.BuildCardItems(state, new[] { "a" });

        Assert.Single(cards);
        Assert.Equal("x: 1, y: 2, value: A", cards[0].Preview);
    }

    [Fact]
    public void NestedArrayItems_Preview_ShowsCompactArrayCount()
    {
        var state = EditorStateFactory.FromJsonString("{\"a\":[[1,2],[3,4]]}");
        var cards = EditorLogic.BuildCardItems(state, new[] { "a" });

        Assert.Equal(2, cards.Count);
        Assert.Equal("[2]", cards[0].Preview);
        Assert.Equal("[2]", cards[1].Preview);
    }
}

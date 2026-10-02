using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.Core.Tests.TestSupport;

namespace Jsonsui.Core.Tests;

public class TreeNavigationTests
{
    private const string Json = """
    {
      "a": { "b": { "x": 1 }, "c": 2 },
      "d": [1, 2, 3],
      "e": { "f": {} }
    }
    """;

    private static (EditorState State, JsonEditorNode Root) Build(bool expandA = false, bool expandE = false)
    {
        var state = EditorStateFactory.FromJsonString(Json);
        if (expandA) state.Expanded.Add("a");
        if (expandE) state.Expanded.Add("e");
        var root = EditorLogic.BuildTree(state, null);
        return (state, root);
    }

    private static string[] Paths(JsonEditorNode root)
        => EditorLogic.FlattenVisibleNavigable(root).Select(n => n.PathKey).ToArray();

    [Fact]
    public void Flatten_WhenCollapsed_ContainsRootAndNavigableChildren()
    {
        var (_, root) = Build();

        Assert.Equal(new[] { "", "a", "d", "e" }, Paths(root));
    }

    [Fact]
    public void Flatten_WhenExpanded_IncludesDescendantsInDisplayOrder()
    {
        var (_, root) = Build(expandA: true, expandE: true);

        Assert.Equal(new[] { "", "a", "a/b", "d", "e", "e/f" }, Paths(root));
    }

    [Fact]
    public void Flatten_ExcludesArrayElements()
    {
        var (_, root) = Build();

        Assert.DoesNotContain("d/0", Paths(root));
    }

    [Fact]
    public void GetNextVisible_MovesForwardAndBackward()
    {
        var (_, root) = Build();

        Assert.Equal("a", EditorLogic.GetNextVisible(root, [], 1)!.PathKey);
        Assert.Equal("e", EditorLogic.GetNextVisible(root, ["d"], 1)!.PathKey);
        Assert.Equal("d", EditorLogic.GetNextVisible(root, ["e"], -1)!.PathKey);
    }

    [Fact]
    public void GetNextVisible_ReturnsNullAtEdges()
    {
        var (_, root) = Build();

        Assert.Null(EditorLogic.GetNextVisible(root, [], -1));
        Assert.Null(EditorLogic.GetNextVisible(root, ["e"], 1));
    }

    [Fact]
    public void GetVisibleParent_ReturnsNearestNavigableAncestor()
    {
        var (_, root) = Build(expandA: true);

        var b = EditorLogic.FindNodeByPath(root, ["a", "b"])!;
        Assert.Equal("a", EditorLogic.GetVisibleParent(b)!.PathKey);

        var a = EditorLogic.FindNodeByPath(root, ["a"])!;
        Assert.Equal("", EditorLogic.GetVisibleParent(a)!.PathKey);
    }

    [Fact]
    public void GetVisibleParent_ReturnsNullForRoot()
    {
        var (_, root) = Build();

        Assert.Null(EditorLogic.GetVisibleParent(root));
    }

    [Fact]
    public void GetFirstVisibleChild_ReturnsNullWhenNotExpandable()
    {
        var (_, root) = Build();

        var array = EditorLogic.FindNodeByPath(root, ["d"])!;
        Assert.Null(EditorLogic.GetFirstVisibleChild(array));
    }

    [Fact]
    public void GetFirstVisibleChild_ReturnsFirstWhenExpanded()
    {
        var (_, root) = Build(expandA: true);

        var a = EditorLogic.FindNodeByPath(root, ["a"])!;
        Assert.Equal("a/b", EditorLogic.GetFirstVisibleChild(a)!.PathKey);
    }

    [Fact]
    public void GetLastVisibleNavigable_ReturnsDeepestLastNode()
    {
        var (_, root) = Build(expandE: true);

        Assert.Equal("e/f", EditorLogic.GetLastVisibleNavigable(root)!.PathKey);
    }
}

using JetBrains.Annotations;
using XiHan.Framework.Utils.DataFilter.Trees;
using XiHan.Framework.Utils.DataFilter.Trees.Dtos;

namespace XiHan.Framework.Utils.Test.DataFilter.Trees;

[TestSubject(typeof(TreeExtensions))]
public class TreeExtensionsTest
{
    [Fact]
    public void ToTree_CreatesTreeStructure()
    {
        var source = new List<int>
        {
            1,
            2,
            3,
            4,
            5
        };
        static bool isChild(int parent, int child) => parent == 1 && child > 1;

        var result = source.ToTree(isChild).ToList();

        _ = Assert.Single(result);
        Assert.Equal(1, result[0].Value);
        Assert.Equal(4, result[0].Children.Count);
    }

    [Fact]
    public void DepthFirstTraversal_TraversesAllNodes()
    {
        var root = new TreeNodeDto<int>(1);
        root.Children.Add(new TreeNodeDto<int>(2));
        root.Children.Add(new TreeNodeDto<int>(3));
        root.Children[0].Children.Add(new TreeNodeDto<int>(4));

        var result = root.DepthFirstTraversal().Select(node => node.Value).ToList();

        Assert.Equal(
        [
            1, 2, 4, 3
        ], result);
    }

    [Fact]
    public void BreadthFirstTraversal_TraversesAllNodes()
    {
        var root = new TreeNodeDto<int>(1);
        root.Children.Add(new TreeNodeDto<int>(2));
        root.Children.Add(new TreeNodeDto<int>(3));
        root.Children[0].Children.Add(new TreeNodeDto<int>(4));

        var result = root.BreadthFirstTraversal().Select(node => node.Value).ToList();

        Assert.Equal(
        [
            1, 2, 3, 4
        ], result);
    }

    [Fact]
    public void FindNode_ReturnsCorrectNode()
    {
        var root = new TreeNodeDto<int>(1);
        var child = new TreeNodeDto<int>(2);
        root.Children.Add(child);

        var result = root.FindNode(2);

        Assert.NotNull(result);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void FindNode_ReturnsNull_WhenNodeNotFound()
    {
        var root = new TreeNodeDto<int>(1);
        root.Children.Add(new TreeNodeDto<int>(2));

        var result = root.FindNode(3);

        Assert.Null(result);
    }

    [Fact]
    public void GetPath_ReturnsCorrectPath()
    {
        var root = new TreeNodeDto<int>(1);
        var child = new TreeNodeDto<int>(2);
        root.Children.Add(child);

        var result = root.GetPath(2);

        Assert.NotNull(result);
        Assert.Equal(
        [
            1, 2
        ], result.Select(node => node.Value).ToList());
    }

    [Fact]
    public void GetPath_ReturnsNull_WhenNodeNotFound()
    {
        var root = new TreeNodeDto<int>(1);
        root.Children.Add(new TreeNodeDto<int>(2));

        var result = root.GetPath(3);

        Assert.Null(result);
    }

    [Fact]
    public void AddChild_AddsChildNode()
    {
        var parent = new TreeNodeDto<int>(1);

        parent.AddChild(2);

        _ = Assert.Single(parent.Children);
        Assert.Equal(2, parent.Children[0].Value);
    }

    [Fact]
    public void RemoveNode_RemovesNode()
    {
        var root = new TreeNodeDto<int>(1);
        var child = new TreeNodeDto<int>(2);
        root.Children.Add(child);

        var result = root.RemoveNode(2);

        Assert.True(result);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void RemoveNode_ReturnsFalse_WhenNodeNotFound()
    {
        var root = new TreeNodeDto<int>(1);
        root.Children.Add(new TreeNodeDto<int>(2));

        var result = root.RemoveNode(3);

        Assert.False(result);
    }

    [Fact]
    public void GetHeight_ReturnsCorrectHeight()
    {
        var root = new TreeNodeDto<int>(1);
        var child = new TreeNodeDto<int>(2);
        root.Children.Add(child);
        child.Children.Add(new TreeNodeDto<int>(3));

        var result = root.GetHeight();

        Assert.Equal(3, result);
    }

    [Fact]
    public void GetLeafNodes_ReturnsAllLeafNodes()
    {
        var root = new TreeNodeDto<int>(1);
        var child1 = new TreeNodeDto<int>(2);
        var child2 = new TreeNodeDto<int>(3);
        root.Children.Add(child1);
        root.Children.Add(child2);
        child1.Children.Add(new TreeNodeDto<int>(4));

        var result = root.GetLeafNodes().Select(node => node.Value).ToList();

        Assert.Equal(
        [
            4, 3
        ], result);
    }
}

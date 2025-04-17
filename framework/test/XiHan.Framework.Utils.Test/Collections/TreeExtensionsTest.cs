using JetBrains.Annotations;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Collections.Tree;

namespace XiHan.Framework.Utils.Test.Collections;

[TestSubject(typeof(TreeExtensions))]
public class TreeExtensionsTest
{
    [Fact]
    public void ToTree_CreatesTreeStructure_WithIsChildFunction()
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
    public void ToTree_CreatesTreeStructure_WithKeySelectors()
    {
        var source = new List<(int Id, int? ParentId)>
        {
            (1, null),
            (2, 1),
            (3, 1),
            (4, 2),
            (5, 2)
        };

        var result = source.ToTree(x => x.Id, x => x.ParentId).ToList();

        _ = Assert.Single(result);
        Assert.Equal(1, result[0].Value.Id);
        Assert.Equal(2, result[0].Children.Count);
        Assert.Equal(2, result[0].Children[0].Value.Id);
        Assert.Equal(2, result[0].Children[0].Children.Count);
    }

    [Fact]
    public void ToTree_ReturnsEmpty_WhenSourceIsEmpty()
    {
        var source = new List<int>();

        static bool isChild(int parent, int child) => parent == 1 && child > 1;

        var result = source.ToTree(isChild);

        Assert.Empty(result);
    }

    [Fact]
    public void ToTree_ReturnsEmpty_WhenNoRootNodes()
    {
        var source = new List<int>
        {
            2, 3, 4, 5
        };

        static bool isChild(int parent, int child) => parent == 1 && child > 1;

        var result = source.ToTree(isChild);

        Assert.Empty(result);
    }

    [Fact]
    public void ToTree_HandlesCyclicDependencies()
    {
        var source = new List<int>
        {
            1, 2, 3
        };

        static bool isChild(int parent, int child) => (parent == 1 && child == 2) || (parent == 2 && child == 3) || (parent == 3 && child == 1);

        var result = source.ToTree(isChild).ToList();

        _ = Assert.Single(result);
        Assert.Equal(1, result[0].Value);
        _ = Assert.Single(result[0].Children);
        Assert.Equal(2, result[0].Children[0].Value);
        _ = Assert.Single(result[0].Children[0].Children);
        Assert.Equal(3, result[0].Children[0].Children[0].Value);
        Assert.Empty(result[0].Children[0].Children[0].Children);
    }

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
        var root = new TreeNode<int>(1);
        root.Children.Add(new TreeNode<int>(2));
        root.Children.Add(new TreeNode<int>(3));
        root.Children[0].Children.Add(new TreeNode<int>(4));

        var result = root.DepthFirstTraversal().Select(node => node.Value).ToList();

        Assert.Equal(
        [
            1, 2, 4, 3
        ], result);
    }

    [Fact]
    public void BreadthFirstTraversal_TraversesAllNodes()
    {
        var root = new TreeNode<int>(1);
        root.Children.Add(new TreeNode<int>(2));
        root.Children.Add(new TreeNode<int>(3));
        root.Children[0].Children.Add(new TreeNode<int>(4));

        var result = root.BreadthFirstTraversal().Select(node => node.Value).ToList();

        Assert.Equal(
        [
            1, 2, 3, 4
        ], result);
    }

    [Fact]
    public void FindNode_ReturnsCorrectNode()
    {
        var root = new TreeNode<int>(1);
        var child = new TreeNode<int>(2);
        root.Children.Add(child);

        var result = root.FindNode(2);

        Assert.NotNull(result);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void FindNode_ReturnsNull_WhenNodeNotFound()
    {
        var root = new TreeNode<int>(1);
        root.Children.Add(new TreeNode<int>(2));

        var result = root.FindNode(3);

        Assert.Null(result);
    }

    [Fact]
    public void GetPath_ReturnsCorrectPath()
    {
        var root = new TreeNode<int>(1);
        var child = new TreeNode<int>(2);
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
        var root = new TreeNode<int>(1);
        root.Children.Add(new TreeNode<int>(2));

        var result = root.GetPath(3);

        Assert.Null(result);
    }

    [Fact]
    public void AddChild_AddsChildNode()
    {
        var parent = new TreeNode<int>(1);

        parent.AddChild(2);

        _ = Assert.Single(parent.Children);
        Assert.Equal(2, parent.Children[0].Value);
    }

    [Fact]
    public void RemoveNode_RemovesNode()
    {
        var root = new TreeNode<int>(1);
        var child = new TreeNode<int>(2);
        root.Children.Add(child);

        var result = root.RemoveNode(2);

        Assert.True(result);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void RemoveNode_ReturnsFalse_WhenNodeNotFound()
    {
        var root = new TreeNode<int>(1);
        root.Children.Add(new TreeNode<int>(2));

        var result = root.RemoveNode(3);

        Assert.False(result);
    }

    [Fact]
    public void GetHeight_ReturnsCorrectHeight()
    {
        var root = new TreeNode<int>(1);
        var child = new TreeNode<int>(2);
        root.Children.Add(child);
        child.Children.Add(new TreeNode<int>(3));

        var result = root.GetHeight();

        Assert.Equal(3, result);
    }

    [Fact]
    public void GetLeafNodes_ReturnsAllLeafNodes()
    {
        var root = new TreeNode<int>(1);
        var child1 = new TreeNode<int>(2);
        var child2 = new TreeNode<int>(3);
        root.Children.Add(child1);
        root.Children.Add(child2);
        child1.Children.Add(new TreeNode<int>(4));

        var result = root.GetLeafNodes().Select(node => node.Value).ToList();

        Assert.Equal(
        [
            4, 3
        ], result);
    }
}

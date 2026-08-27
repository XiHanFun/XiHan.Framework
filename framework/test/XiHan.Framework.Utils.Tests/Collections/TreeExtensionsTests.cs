// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 树扩展方法测试
/// </summary>
/// <remarks>
/// 统一使用四个节点的固定树形：1 为根，2、3 挂在 1 下，4 挂在 2 下。
/// </remarks>
public class TreeExtensionsTests
{
    /// <summary>
    /// 按主键与父键构建树，只返回根节点
    /// </summary>
    [Fact]
    public void ToTree_ByKeySelectors_BuildsHierarchyAndReturnsRoots()
    {
        var roots = BuildTreeByKeys();

        var root = Assert.Single(roots);
        Assert.Equal(1, root.Value.Id);
        Assert.Equal(2, root.Children.Count);
        Assert.Contains(root.Children, child => child.Value.Id == 2);
        Assert.Contains(root.Children, child => child.Value.Id == 3);
    }

    /// <summary>
    /// 按父子判定函数构建树，得到同样的层级
    /// </summary>
    [Fact]
    public void ToTree_ByIsChildPredicate_BuildsSameHierarchy()
    {
        var roots = CreateItems().ToTree((parent, child) => child.ParentId == parent.Id).ToList();

        var root = Assert.Single(roots);
        Assert.Equal(1, root.Value.Id);
        Assert.Equal(2, root.Children.Count);

        var second = root.Children.Single(child => child.Value.Id == 2);
        Assert.Equal(4, second.Children.Single().Value.Id);
    }

    /// <summary>
    /// 空数据集构建出空树
    /// </summary>
    [Fact]
    public void ToTree_WhenSourceEmpty_ReturnsEmpty()
    {
        Assert.Empty(Array.Empty<Item>().ToTree(x => x.Id, x => x.ParentId));
        Assert.Empty(Array.Empty<Item>().ToTree((parent, child) => child.ParentId == parent.Id));
    }

    /// <summary>
    /// 给节点添加子节点
    /// </summary>
    [Fact]
    public void AddChild_OnNode_AppendsChild()
    {
        var node = new TreeNode<Item>(new Item(1, 0));

        node.AddChild(new Item(2, 1));

        Assert.Equal(2, node.Children.Single().Value.Id);
    }

    /// <summary>
    /// 父节点为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void AddChild_OnNode_WhenParentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TreeExtensions.AddChild<Item>(null!, new Item(1, 0)));
    }

    /// <summary>
    /// 树中找不到父节点时抛无效操作异常
    /// </summary>
    [Fact]
    public void AddChild_OnTree_WhenParentNotFound_Throws()
    {
        var roots = BuildTreeByKeys();

        Assert.Throws<InvalidOperationException>(() =>
            roots.AddChild(new Item(99, 0), new Item(100, 99), x => x.Id, x => x.ParentId));
    }

    /// <summary>
    /// 父节点或子节点为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void AddChild_OnTree_WhenArgumentIsNull_Throws()
    {
        var roots = BuildTreeByKeys();

        Assert.Throws<ArgumentNullException>(() =>
            roots.AddChild(null!, new Item(100, 99), x => x.Id, x => x.ParentId));
        Assert.Throws<ArgumentNullException>(() =>
            roots.AddChild(new Item(1, 0), null!, x => x.Id, x => x.ParentId));
    }

    /// <summary>
    /// 删除深层节点返回真，节点从父节点上摘除
    /// </summary>
    [Fact]
    public void RemoveNode_WhenNestedNodeExists_RemovesAndReturnsTrue()
    {
        var root = BuildTreeByKeys().Single();

        var removed = root.RemoveNode(new Item(4, 2));

        Assert.True(removed);
        Assert.Empty(root.Children.Single(child => child.Value.Id == 2).Children);
    }

    /// <summary>
    /// 删除不存在的节点返回假
    /// </summary>
    [Fact]
    public void RemoveNode_WhenNodeMissing_ReturnsFalse()
    {
        var root = BuildTreeByKeys().Single();

        Assert.False(root.RemoveNode(new Item(999, 0)));
    }

    /// <summary>
    /// 根节点为 null 时返回假
    /// </summary>
    [Fact]
    public void RemoveNode_WhenRootIsNull_ReturnsFalse()
    {
        Assert.False(TreeExtensions.RemoveNode(null, new Item(1, 0)));
    }

    /// <summary>
    /// 深度优先按"根、左子树、右子树"顺序遍历
    /// </summary>
    [Fact]
    public void DepthFirstTraversal_VisitsRootThenEachSubtree()
    {
        var root = BuildTreeByKeys().Single();

        var ids = root.DepthFirstTraversal().Select(node => node.Value.Id).ToArray();

        Assert.Equal(new[] { 1, 2, 4, 3 }, ids);
    }

    /// <summary>
    /// 集合重载遍历所有根节点下的全部节点
    /// </summary>
    [Fact]
    public void DepthFirstTraversal_OnCollection_VisitsAllNodes()
    {
        var roots = BuildTreeByKeys();

        var ids = roots.DepthFirstTraversal().Select(node => node.Value.Id).ToArray();

        Assert.Equal(new[] { 1, 2, 4, 3 }, ids);
    }

    /// <summary>
    /// 入参为 null 时遍历得到空序列
    /// </summary>
    [Fact]
    public void DepthFirstTraversal_WhenNull_ReturnsEmpty()
    {
        Assert.Empty(TreeExtensions.DepthFirstTraversal<Item>((TreeNode<Item>?)null));
        Assert.Empty(TreeExtensions.DepthFirstTraversal<Item>((IEnumerable<TreeNode<Item>>?)null));
    }

    /// <summary>
    /// 广度优先按层级顺序遍历
    /// </summary>
    [Fact]
    public void BreadthFirstTraversal_VisitsLevelByLevel()
    {
        var root = BuildTreeByKeys().Single();

        var ids = root.BreadthFirstTraversal().Select(node => node.Value.Id).ToArray();

        Assert.Equal(new[] { 1, 2, 3, 4 }, ids);
    }

    /// <summary>
    /// 根节点为 null 时广度优先得到空序列
    /// </summary>
    [Fact]
    public void BreadthFirstTraversal_WhenNull_ReturnsEmpty()
    {
        Assert.Empty(TreeExtensions.BreadthFirstTraversal<Item>(null));
    }

    /// <summary>
    /// 查找节点命中返回对应节点，未命中返回 null
    /// </summary>
    [Fact]
    public void FindNode_ReturnsMatchedNodeOrNull()
    {
        var root = BuildTreeByKeys().Single();

        var found = root.FindNode(new Item(4, 2));

        Assert.NotNull(found);
        Assert.Equal(4, found!.Value.Id);
        Assert.Null(root.FindNode(new Item(999, 0)));
    }

    /// <summary>
    /// 取路径返回从根到目标的完整链路
    /// </summary>
    [Fact]
    public void GetPath_ReturnsChainFromRootToTarget()
    {
        var root = BuildTreeByKeys().Single();

        var path = root.GetPath(new Item(4, 2));

        Assert.NotNull(path);
        Assert.Equal(new[] { 1, 2, 4 }, path!.Select(node => node.Value.Id));
    }

    /// <summary>
    /// 目标不存在时路径为 null
    /// </summary>
    [Fact]
    public void GetPath_WhenTargetMissing_ReturnsNull()
    {
        var root = BuildTreeByKeys().Single();

        Assert.Null(root.GetPath(new Item(999, 0)));
    }

    /// <summary>
    /// 树高按最长分支计算，空树为 0
    /// </summary>
    [Fact]
    public void GetHeight_CountsLongestBranch()
    {
        var root = BuildTreeByKeys().Single();

        Assert.Equal(3, root.GetHeight());
        Assert.Equal(1, new TreeNode<Item>(new Item(1, 0)).GetHeight());
        Assert.Equal(0, TreeExtensions.GetHeight<Item>(null));
    }

    /// <summary>
    /// 叶子节点是所有没有子节点的节点
    /// </summary>
    [Fact]
    public void GetLeafNodes_ReturnsNodesWithoutChildren()
    {
        var root = BuildTreeByKeys().Single();

        var leafIds = root.GetLeafNodes().Select(node => node.Value.Id).OrderBy(id => id).ToArray();

        Assert.Equal(new[] { 3, 4 }, leafIds);
    }

    /// <summary>
    /// 构造固定的四节点测试数据
    /// </summary>
    private static Item[] CreateItems()
    {
        return
        [
            new Item(1, 0),
            new Item(2, 1),
            new Item(3, 1),
            new Item(4, 2)
        ];
    }

    /// <summary>
    /// 用主键、父键选择器构建固定树形并返回根集合
    /// </summary>
    private static List<TreeNode<Item>> BuildTreeByKeys()
    {
        return [.. CreateItems().ToTree(x => x.Id, x => x.ParentId)];
    }

    /// <summary>
    /// 测试用树节点数据
    /// </summary>
    private sealed record Item(int Id, int ParentId);
}

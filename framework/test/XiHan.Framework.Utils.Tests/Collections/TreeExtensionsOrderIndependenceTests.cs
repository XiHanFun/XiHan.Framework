// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 树扩展方法按父子判定函数建树的顺序无关性回归测试
/// </summary>
/// <remarks>
/// 原实现边遍历边挂链，并用一个跨遍历共享的 visited 集合兼做环检测：
/// 子节点若排在父节点前面，会先被当作根遍历一遍并记入 visited，
/// 等父节点再遍历到它时就撞上 visited 判定，对一棵正常的树抛出并不存在的"循环依赖"。
/// 数据库按 id 或排序字段查出来的列表并不保证父一定排在子前面，命中概率很高。
/// 本文件把"建树结果与源集合顺序无关"和"真正的环仍然要抛"两条同时锁住。
/// </remarks>
public class TreeExtensionsOrderIndependenceTests
{
    /// <summary>
    /// 子节点排在父节点前面时同样能正确建树
    /// </summary>
    [Fact]
    public void ToTree_ByIsChildPredicate_WhenChildPrecedesParent_BuildsTree()
    {
        Item[] items =
        [
            new(4, 2),
            new(2, 1),
            new(3, 1),
            new(1, 0)
        ];

        var roots = items.ToTree((parent, child) => child.ParentId == parent.Id).ToList();

        var root = Assert.Single(roots);
        Assert.Equal(1, root.Value.Id);
        Assert.Equal(new[] { 2, 3 }, root.Children.Select(child => child.Value.Id).OrderBy(id => id));
        Assert.Equal(4, root.Children.Single(child => child.Value.Id == 2).Children.Single().Value.Id);
    }

    /// <summary>
    /// 源集合的任意排列都得到同一棵树
    /// </summary>
    [Fact]
    public void ToTree_ByIsChildPredicate_IsIndependentOfSourceOrder()
    {
        Item[] items = [new(1, 0), new(2, 1), new(3, 1), new(4, 2)];

        foreach (var permutation in Permute(items))
        {
            var roots = permutation.ToTree((parent, child) => child.ParentId == parent.Id).ToList();

            var root = Assert.Single(roots);
            Assert.Equal(1, root.Value.Id);
            Assert.Equal(3, root.DepthFirstTraversal().Count() - 1);
            Assert.Equal(3, root.GetHeight());
        }
    }

    /// <summary>
    /// 真正成环时仍然抛无效操作异常
    /// </summary>
    [Fact]
    public void ToTree_ByIsChildPredicate_WhenCycleExists_Throws()
    {
        Item[] items = [new(1, 2), new(2, 1)];

        Assert.Throws<InvalidOperationException>(() =>
            items.ToTree((parent, child) => child.ParentId == parent.Id).ToList());
    }

    /// <summary>
    /// 节点以自己为父时按成环处理
    /// </summary>
    [Fact]
    public void ToTree_ByIsChildPredicate_WhenSelfReferenced_Throws()
    {
        Item[] items = [new(1, 1)];

        Assert.Throws<InvalidOperationException>(() =>
            items.ToTree((parent, child) => child.ParentId == parent.Id).ToList());
    }

    /// <summary>
    /// 同一节点被多个父节点引用不算环，不应抛异常
    /// </summary>
    /// <remarks>
    /// 判定函数可以把一个节点判给多个父节点，这不是环，只是非树形的多父结构；
    /// 原实现会把它误报成"循环依赖"，现在按各父节点分别挂链，根集合中不包含被引用的节点。
    /// </remarks>
    [Fact]
    public void ToTree_ByIsChildPredicate_WhenNodeHasMultipleParents_DoesNotThrow()
    {
        Item[] items = [new(1, 0), new(2, 0), new(3, 1)];

        var roots = items.ToTree((parent, child) => child.Id == 3 && parent.Id is 1 or 2).ToList();

        Assert.Equal(new[] { 1, 2 }, roots.Select(root => root.Value.Id).OrderBy(id => id));
        Assert.All(roots, root => Assert.Equal(3, root.Children.Single().Value.Id));
    }

    /// <summary>
    /// 入参为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void ToTree_ByIsChildPredicate_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TreeExtensions.ToTree<Item>(null!, (parent, child) => child.ParentId == parent.Id).ToList());
        Assert.Throws<ArgumentNullException>(() =>
            Array.Empty<Item>().ToTree((Func<Item, Item, bool>)null!).ToList());
    }

    /// <summary>
    /// 生成集合的全排列
    /// </summary>
    private static IEnumerable<Item[]> Permute(Item[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (var i = 0; i < items.Length; i++)
        {
            var head = items[i];
            var rest = items.Where((_, index) => index != i).ToArray();
            foreach (var tail in Permute(rest))
            {
                yield return [head, .. tail];
            }
        }
    }

    /// <summary>
    /// 测试用树节点数据
    /// </summary>
    private sealed record Item(int Id, int ParentId);
}

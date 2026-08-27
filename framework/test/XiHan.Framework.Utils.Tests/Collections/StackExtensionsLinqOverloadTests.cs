// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 堆栈扩展方法中与 LINQ 同名重载的回归测试
/// </summary>
/// <remarks>
/// 锁两类缺陷：
/// 一是 Contains/Count/Where/Select/All/Any 内部写 stack.Xxx(...) 会解析回自身（Stack&lt;T&gt; 是恒等转换，
/// 优先于 Enumerable 的 Stack&lt;T&gt;→IEnumerable&lt;T&gt; 引用转换），调用即无限递归、栈溢出杀进程；
/// 二是 Clone 用 new Stack&lt;T&gt;(stack.ToArray()) 会把栈顶栈底颠倒，与注释承诺的"保持原始堆栈的顺序"相反。
/// 用例里的 stack.Xxx(...) 调用点本身也在复现缺陷路径：它们绑定的正是 StackExtensions 的重载。
/// </remarks>
public class StackExtensionsLinqOverloadTests
{
    /// <summary>
    /// 构造一个自底向上压入 1、2、3 的堆栈（栈顶为 3）
    /// </summary>
    private static Stack<int> BuildStack()
    {
        var stack = new Stack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        return stack;
    }

    /// <summary>
    /// 带谓词的 Contains 正常返回，不会自递归
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Contains_WithPredicate_ReturnsResultWithoutSelfRecursion()
    {
        var stack = BuildStack();

        Assert.True(stack.Contains(x => x == 2));
        Assert.False(stack.Contains(x => x == 99));
    }

    /// <summary>
    /// 带谓词的 Count 正常返回，不会自递归
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Count_WithPredicate_ReturnsMatchedCountWithoutSelfRecursion()
    {
        var stack = BuildStack();

        Assert.Equal(2, stack.Count(x => x > 1));
        Assert.Equal(0, stack.Count(x => x < 0));
    }

    /// <summary>
    /// 带谓词的 All 正常返回，不会自递归
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void All_WithPredicate_ReturnsResultWithoutSelfRecursion()
    {
        var stack = BuildStack();

        Assert.True(stack.All(x => x > 0));
        Assert.False(stack.All(x => x > 1));
    }

    /// <summary>
    /// 带谓词的 Any 正常返回，不会自递归
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Any_WithPredicate_ReturnsResultWithoutSelfRecursion()
    {
        var stack = BuildStack();

        Assert.True(stack.Any(x => x == 3));
        Assert.False(stack.Any(x => x == 4));
    }

    /// <summary>
    /// Where 过滤后仍保持源堆栈的枚举顺序（栈顶在前）
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Where_FiltersAndKeepsSourceEnumerationOrder()
    {
        var stack = BuildStack();

        var filtered = stack.Where(x => x % 2 == 1);

        Assert.Equal(new[] { 3, 1 }, filtered.ToArray());
        Assert.Equal(3, filtered.Peek());
    }

    /// <summary>
    /// Select 投影后仍保持源堆栈的枚举顺序（栈顶在前）
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Select_ProjectsAndKeepsSourceEnumerationOrder()
    {
        var stack = BuildStack();

        var projected = stack.Select(x => x * 10);

        Assert.Equal(new[] { 30, 20, 10 }, projected.ToArray());
        Assert.Equal(30, projected.Peek());
    }

    /// <summary>
    /// Where/Select 不会改动源堆栈
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void WhereAndSelect_DoNotMutateSource()
    {
        var stack = BuildStack();

        _ = stack.Where(x => x > 0);
        _ = stack.Select(x => x.ToString());

        Assert.Equal(new[] { 3, 2, 1 }, stack.ToArray());
    }

    /// <summary>
    /// Clone 保持原堆栈顺序，栈顶仍是栈顶
    /// </summary>
    [Fact]
    public void Clone_KeepsTopAtTop()
    {
        var stack = BuildStack();

        var clone = stack.Clone();

        Assert.Equal(3, clone.Peek());
        Assert.Equal(new[] { 3, 2, 1 }, clone.ToArray());
    }

    /// <summary>
    /// Clone 与 DeepClone 对同一源堆栈给出一致的顺序
    /// </summary>
    [Fact]
    public void Clone_MatchesDeepCloneOrder()
    {
        var stack = BuildStack();

        Assert.Equal(stack.DeepClone().ToArray(), stack.Clone().ToArray());
    }

    /// <summary>
    /// Clone 出来的是独立副本，改动互不影响
    /// </summary>
    [Fact]
    public void Clone_ReturnsIndependentCopy()
    {
        var stack = BuildStack();

        var clone = stack.Clone();
        clone.Push(4);

        Assert.Equal(3, stack.Count);
        Assert.Equal(4, clone.Count);
    }

    /// <summary>
    /// 空堆栈的克隆与谓词重载都能正常工作
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EmptyStack_CloneAndPredicateOverloads_Work()
    {
        var stack = new Stack<int>();

        Assert.Empty(stack.Clone());
        Assert.False(stack.Any(x => true));
        Assert.True(stack.All(x => false));
        Assert.Equal(0, stack.Count(x => true));
        Assert.False(stack.Contains(x => true));
    }
}

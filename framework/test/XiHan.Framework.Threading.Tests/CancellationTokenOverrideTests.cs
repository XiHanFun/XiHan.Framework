// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Threading.Tests;

/// <summary>
/// 令牌重写载体测试
/// </summary>
/// <remarks>
/// 该类型是只读载体：构造时收下令牌，之后不可变，并且保持与原取消源的联动。
/// 它是普通类而非记录，两个持有同一令牌的实例仍按引用比较互不相等，
/// 用例锁定这一点，避免后续误改为记录类型后影响作用域栈里按引用识别的行为。
/// </remarks>
public class CancellationTokenOverrideTests
{
    /// <summary>
    /// 构造时原样保存传入的令牌
    /// </summary>
    [Fact]
    public void Constructor_StoresGivenToken()
    {
        using var source = new CancellationTokenSource();

        var sut = new CancellationTokenOverride(source.Token);

        Assert.Equal(source.Token, sut.CancellationToken);
        Assert.True(sut.CancellationToken.CanBeCanceled);
    }

    /// <summary>
    /// 传入空令牌时保存空令牌
    /// </summary>
    [Fact]
    public void Constructor_WithNoneToken_StoresNoneToken()
    {
        var sut = new CancellationTokenOverride(CancellationToken.None);

        Assert.Equal(CancellationToken.None, sut.CancellationToken);
        Assert.False(sut.CancellationToken.CanBeCanceled);
        Assert.False(sut.CancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// 保存下来的令牌继续跟随原取消源的取消状态
    /// </summary>
    [Fact]
    public void CancellationToken_FollowsSourceCancellation()
    {
        using var source = new CancellationTokenSource();
        var sut = new CancellationTokenOverride(source.Token);

        Assert.False(sut.CancellationToken.IsCancellationRequested);

        source.Cancel();

        Assert.True(sut.CancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// 两个持有相同令牌的实例按引用比较，互不相等
    /// </summary>
    [Fact]
    public void Instances_WithSameToken_AreNotEqual()
    {
        using var source = new CancellationTokenSource();

        var first = new CancellationTokenOverride(source.Token);
        var second = new CancellationTokenOverride(source.Token);

        Assert.NotSame(first, second);
        Assert.NotEqual(first, second);
        Assert.Equal(first.CancellationToken, second.CancellationToken);
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Repositories;

namespace XiHan.Framework.Domain.Tests.Repositories;

/// <summary>
/// 写路径租户边界守卫测试
/// </summary>
/// <remarks>
/// 守卫豁免走 AsyncLocal：必须「只在当前异步流内生效、作用域结束自动还原、子流的开启不上浮」。
/// 任何一条不成立都会让跨租户写入的拦截出现漏洞，所以这三条都要单独验证。
/// </remarks>
public class TenantWriteGuardTests
{
    /// <summary>
    /// 默认状态下守卫未被豁免
    /// </summary>
    [Fact]
    public void IsSuppressed_ByDefault_IsFalse()
    {
        Assert.False(TenantWriteGuard.IsSuppressed);
    }

    /// <summary>
    /// 作用域内豁免生效，作用域结束后自动还原
    /// </summary>
    [Fact]
    public void Suppress_WithinScope_EnablesAndRestores()
    {
        using (TenantWriteGuard.Suppress())
        {
            Assert.True(TenantWriteGuard.IsSuppressed);
        }

        Assert.False(TenantWriteGuard.IsSuppressed);
    }

    /// <summary>
    /// 嵌套作用域退出内层时还原为外层状态而不是直接关闭
    /// </summary>
    [Fact]
    public void Suppress_WhenNested_RestoresToOuterState()
    {
        var outer = TenantWriteGuard.Suppress();
        Assert.True(TenantWriteGuard.IsSuppressed);

        var inner = TenantWriteGuard.Suppress();
        Assert.True(TenantWriteGuard.IsSuppressed);

        inner.Dispose();
        Assert.True(TenantWriteGuard.IsSuppressed);

        outer.Dispose();
        Assert.False(TenantWriteGuard.IsSuppressed);
    }

    /// <summary>
    /// 重复释放同一作用域不会误关掉之后新开的作用域
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var scope = TenantWriteGuard.Suppress();
        scope.Dispose();
        Assert.False(TenantWriteGuard.IsSuppressed);

        using var later = TenantWriteGuard.Suppress();
        scope.Dispose();

        // 已释放的旧句柄再次 Dispose 必须是空操作，否则会把新作用域一起关掉
        Assert.True(TenantWriteGuard.IsSuppressed);
    }

    /// <summary>
    /// 豁免状态向下游异步流传播
    /// </summary>
    [Fact]
    public async Task Suppress_FlowsIntoChildAsyncFlow()
    {
        using (TenantWriteGuard.Suppress())
        {
            var observed = await Task.Run(() => TenantWriteGuard.IsSuppressed, TestContext.Current.CancellationToken);

            Assert.True(observed);
        }

        Assert.False(TenantWriteGuard.IsSuppressed);
    }

    /// <summary>
    /// 子异步流内开启的豁免不会上浮到父流
    /// </summary>
    [Fact]
    public async Task Suppress_InChildAsyncFlow_DoesNotLeakToParent()
    {
        var observedInChild = await Task.Run(
            () =>
            {
                using (TenantWriteGuard.Suppress())
                {
                    return TenantWriteGuard.IsSuppressed;
                }
            },
            TestContext.Current.CancellationToken);

        Assert.True(observedInChild);
        Assert.False(TenantWriteGuard.IsSuppressed);
    }

    /// <summary>
    /// 并行执行流之间互不干扰
    /// </summary>
    [Fact]
    public async Task Suppress_AcrossParallelFlows_IsIsolated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource();

        var suppressing = Task.Run(
            async () =>
            {
                using (TenantWriteGuard.Suppress())
                {
                    gate.SetResult();
                    await Task.Delay(20, cancellationToken);
                    return TenantWriteGuard.IsSuppressed;
                }
            },
            cancellationToken);

        var observing = Task.Run(
            async () =>
            {
                await gate.Task;
                return TenantWriteGuard.IsSuppressed;
            },
            cancellationToken);

        var results = await Task.WhenAll(suppressing, observing);

        Assert.True(results[0]);
        Assert.False(results[1]);
    }

    /// <summary>
    /// 每次开启豁免都返回独立的作用域句柄
    /// </summary>
    [Fact]
    public void Suppress_ReturnsDistinctScopeInstances()
    {
        using var first = TenantWriteGuard.Suppress();
        using var second = TenantWriteGuard.Suppress();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }
}

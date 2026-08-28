// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 基于 AsyncLocal 的当前租户访问器的测试
/// </summary>
/// <remarks>
/// 该访问器是进程级单例且状态挂在 AsyncLocal 上，所以用例遵守两条纪律：
/// 一是整个测试类归入串行集合，二是所有写入都发生在 <c>Task.Run</c> 派生的独立执行上下文里。
/// 后者同时也正好是被测契约本身——写入只沿着当前这一支执行流向下传播，不会回流到调用方。
/// </remarks>
[Collection(TenantContextCollection.Name)]
public class AsyncLocalCurrentTenantAccessorTests
{
    /// <summary>
    /// 单例入口每次返回同一个实例
    /// </summary>
    [Fact]
    public void Instance_IsAlwaysTheSameObject()
    {
        Assert.Same(AsyncLocalCurrentTenantAccessor.Instance, AsyncLocalCurrentTenantAccessor.Instance);
    }

    /// <summary>
    /// 单例实现了当前租户访问器契约
    /// </summary>
    [Fact]
    public void Instance_ImplementsAccessorContract()
    {
        Assert.IsAssignableFrom<ICurrentTenantAccessor>(AsyncLocalCurrentTenantAccessor.Instance);
    }

    /// <summary>
    /// 不对外暴露公共构造函数，只能经由单例入口获取
    /// </summary>
    /// <remarks>
    /// 一旦允许 new 出第二个实例，不同实例之间的 AsyncLocal 互不可见，
    /// 中间件写入的租户在业务层就会读不到；这条契约必须钉死。
    /// </remarks>
    [Fact]
    public void Type_ExposesNoPublicConstructor()
    {
        Assert.Empty(typeof(AsyncLocalCurrentTenantAccessor).GetConstructors());
    }

    /// <summary>
    /// 在独立执行流中写入后可以立即读回
    /// </summary>
    [Fact]
    public async Task Current_SetInIsolatedFlow_IsReadBack()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accessor = AsyncLocalCurrentTenantAccessor.Instance;

        var observed = await Task.Run(() =>
        {
            accessor.Current = new BasicTenantInfo(42L, "租户四二");
            return accessor.Current;
        }, cancellationToken);

        Assert.NotNull(observed);
        Assert.Equal<long?>(42L, observed.TenantId);
        Assert.Equal("租户四二", observed.Name);
    }

    /// <summary>
    /// 置为 null 后回到无租户状态
    /// </summary>
    [Fact]
    public async Task Current_SetToNull_ClearsTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accessor = AsyncLocalCurrentTenantAccessor.Instance;

        var observed = await Task.Run(() =>
        {
            accessor.Current = new BasicTenantInfo(42L);
            accessor.Current = null;
            return accessor.Current;
        }, cancellationToken);

        Assert.Null(observed);
    }

    /// <summary>
    /// 写入沿着当前执行流传播到 await 之后与派生任务
    /// </summary>
    [Fact]
    public async Task Current_SetBeforeAwait_FlowsIntoContinuationsAndChildTasks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accessor = AsyncLocalCurrentTenantAccessor.Instance;

        var observed = await Task.Run(async () =>
        {
            accessor.Current = new BasicTenantInfo(8L, "租户八");

            await Task.Yield();
            var afterYield = accessor.Current?.TenantId;

            await Task.Delay(1, cancellationToken);
            var afterDelay = accessor.Current?.TenantId;

            var inChild = await Task.Run(() => accessor.Current?.TenantId, cancellationToken);

            return (afterYield, afterDelay, inChild);
        }, cancellationToken);

        Assert.Equal<long?>(8L, observed.afterYield);
        Assert.Equal<long?>(8L, observed.afterDelay);
        Assert.Equal<long?>(8L, observed.inChild);
    }

    /// <summary>
    /// 子执行流中的写入不会回流到调用方
    /// </summary>
    /// <remarks>
    /// AsyncLocal 的写时复制语义：只向下传播，不向上回流。
    /// 这正是租户上下文能在并发请求之间互不串扰的根本原因。
    /// </remarks>
    [Fact]
    public async Task Current_SetInChildFlow_DoesNotLeakToCaller()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accessor = AsyncLocalCurrentTenantAccessor.Instance;

        var observedInChild = await Task.Run(() =>
        {
            accessor.Current = new BasicTenantInfo(99L, "租户九九");
            return accessor.Current?.TenantId;
        }, cancellationToken);

        Assert.Equal<long?>(99L, observedInChild);
        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 并行执行流各自持有互不干扰的租户信息
    /// </summary>
    /// <remarks>
    /// 带整体超时保护，任何一支卡住都会在 30 秒内失败而不是让 CI 挂死。
    /// </remarks>
    [Fact]
    public async Task Current_InParallelFlows_AreIsolated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accessor = AsyncLocalCurrentTenantAccessor.Instance;

        async Task<long?> ObserveAsync(long tenantId)
        {
            return await Task.Run(async () =>
            {
                accessor.Current = new BasicTenantInfo(tenantId, $"租户{tenantId}");
                await Task.Delay(5, cancellationToken);
                return accessor.Current?.TenantId;
            }, cancellationToken);
        }

        var tasks = Enumerable.Range(1, 8).Select(index => ObserveAsync(index)).ToArray();
        var observed = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal(8, observed.Length);
        for (var index = 0; index < observed.Length; index++)
        {
            Assert.Equal<long?>(index + 1, observed[index]);
        }

        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 同一执行流中的后续写入覆盖前一次写入
    /// </summary>
    [Fact]
    public async Task Current_SetTwiceInSameFlow_KeepsLastValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accessor = AsyncLocalCurrentTenantAccessor.Instance;

        var observed = await Task.Run(() =>
        {
            accessor.Current = new BasicTenantInfo(1L, "租户一");
            accessor.Current = new BasicTenantInfo(2L, "租户二");
            return accessor.Current;
        }, cancellationToken);

        Assert.NotNull(observed);
        Assert.Equal<long?>(2L, observed.TenantId);
        Assert.Equal("租户二", observed.Name);
    }
}

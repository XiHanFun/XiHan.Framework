// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Tests;

/// <summary>
/// 当前租户的测试
/// </summary>
/// <remarks>
/// 覆盖两层契约：
/// 一是作用域语义——Change 写入访问器、释放器还原到进入前的那一份快照、嵌套按后进先出还原，
/// 这部分用普通字段访问器（<see cref="FakeCurrentTenantAccessor"/>）验证，排除执行上下文干扰；
/// 二是搭配真实 <see cref="AsyncLocalCurrentTenantAccessor"/> 时的跨 await 传播与并行隔离，
/// 这是多租户数据隔离的地基，一旦失效会直接造成跨租户串数据，必须显式钉死。
/// </remarks>
[Collection(TenantContextCollection.Name)]
public class CurrentTenantTests
{
    /// <summary>
    /// 未进入任何作用域时处于宿主上下文
    /// </summary>
    [Fact]
    public void Id_WithoutAnyScope_IsNullAndUnavailable()
    {
        var currentTenant = CreateWithFakeAccessor();

        Assert.Null(currentTenant.Id);
        Assert.Null(currentTenant.Name);
        Assert.False(currentTenant.IsAvailable);
    }

    /// <summary>
    /// 切换后唯一标识与名称同时生效
    /// </summary>
    [Fact]
    public void Change_WithIdAndName_ExposesBothValues()
    {
        var currentTenant = CreateWithFakeAccessor();

        using (currentTenant.Change(9L, "曦寒租户"))
        {
            Assert.True(currentTenant.IsAvailable);
            Assert.Equal<long?>(9L, currentTenant.Id);
            Assert.Equal("曦寒租户", currentTenant.Name);
        }
    }

    /// <summary>
    /// 名称参数可省略，省略时名称为 null
    /// </summary>
    [Fact]
    public void Change_WithoutName_LeavesNameNull()
    {
        var currentTenant = CreateWithFakeAccessor();

        using (currentTenant.Change(9L))
        {
            Assert.Equal<long?>(9L, currentTenant.Id);
            Assert.Null(currentTenant.Name);
        }
    }

    /// <summary>
    /// 任意非 null 唯一标识都算租户可用，包括 0 与负数
    /// </summary>
    /// <remarks>
    /// 可用性只看 null 与否，不看数值大小；把 0 当作「无租户」是常见误实现，这里显式钉死。
    /// </remarks>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public void Change_WithAnyNonNullId_MakesTenantAvailable(long tenantId)
    {
        var currentTenant = CreateWithFakeAccessor();

        using (currentTenant.Change(tenantId))
        {
            Assert.True(currentTenant.IsAvailable);
            Assert.Equal<long?>(tenantId, currentTenant.Id);
        }
    }

    /// <summary>
    /// 切换动作直接落在访问器上，写入的是一份基本租户信息
    /// </summary>
    [Fact]
    public void Change_WritesBasicTenantInfoToAccessor()
    {
        var accessor = new FakeCurrentTenantAccessor();
        var currentTenant = new CurrentTenant(accessor);

        using (currentTenant.Change(5L, "曦寒租户"))
        {
            var scoped = accessor.Current;

            Assert.NotNull(scoped);
            Assert.Equal<long?>(5L, scoped.TenantId);
            Assert.Equal("曦寒租户", scoped.Name);
        }

        Assert.Null(accessor.Current);
    }

    /// <summary>
    /// 释放作用域后回到进入前的宿主上下文
    /// </summary>
    [Fact]
    public void Change_Disposed_RestoresHostContext()
    {
        var currentTenant = CreateWithFakeAccessor();

        var scope = currentTenant.Change(9L, "曦寒租户");
        Assert.True(currentTenant.IsAvailable);

        scope.Dispose();

        Assert.Null(currentTenant.Id);
        Assert.Null(currentTenant.Name);
        Assert.False(currentTenant.IsAvailable);
    }

    /// <summary>
    /// 嵌套切换按后进先出逐层还原
    /// </summary>
    [Fact]
    public void Change_Nested_RestoresInLastInFirstOutOrder()
    {
        var currentTenant = CreateWithFakeAccessor();

        using (currentTenant.Change(1L, "租户一"))
        {
            Assert.Equal<long?>(1L, currentTenant.Id);

            using (currentTenant.Change(2L, "租户二"))
            {
                Assert.Equal<long?>(2L, currentTenant.Id);

                using (currentTenant.Change(3L, "租户三"))
                {
                    Assert.Equal<long?>(3L, currentTenant.Id);
                    Assert.Equal("租户三", currentTenant.Name);
                }

                Assert.Equal<long?>(2L, currentTenant.Id);
                Assert.Equal("租户二", currentTenant.Name);
            }

            Assert.Equal<long?>(1L, currentTenant.Id);
            Assert.Equal("租户一", currentTenant.Name);
        }

        Assert.Null(currentTenant.Id);
    }

    /// <summary>
    /// 租户作用域内切换到 null 表示临时回到宿主，释放后仍还原为原租户
    /// </summary>
    /// <remarks>
    /// 这是跨租户的平台级操作（例如宿主侧查询全局数据）最关键的一条路径，还原失败会直接造成数据串租户。
    /// </remarks>
    [Fact]
    public void Change_ToNullInsideTenantScope_TemporarilySwitchesToHost()
    {
        var currentTenant = CreateWithFakeAccessor();

        using (currentTenant.Change(1L, "租户一"))
        {
            using (currentTenant.Change(null))
            {
                Assert.False(currentTenant.IsAvailable);
                Assert.Null(currentTenant.Id);
                Assert.Null(currentTenant.Name);
            }

            Assert.True(currentTenant.IsAvailable);
            Assert.Equal<long?>(1L, currentTenant.Id);
            Assert.Equal("租户一", currentTenant.Name);
        }
    }

    /// <summary>
    /// 访问器中已存在租户时，释放作用域还原的是那一份既有值而非 null
    /// </summary>
    /// <remarks>
    /// 直接给访问器预置值，模拟中间件在进入业务代码前就解析好了租户的场景：
    /// 释放器必须还原到「进入作用域之前」的快照，而不是无脑清空。
    /// </remarks>
    [Fact]
    public void Change_WhenAccessorAlreadyHasTenant_RestoresPreExistingValue()
    {
        var accessor = new FakeCurrentTenantAccessor
        {
            Current = new BasicTenantInfo(100L, "中间件解析出的租户")
        };
        var currentTenant = new CurrentTenant(accessor);

        using (currentTenant.Change(200L, "临时租户"))
        {
            Assert.Equal<long?>(200L, currentTenant.Id);
        }

        Assert.Equal<long?>(100L, currentTenant.Id);
        Assert.Equal("中间件解析出的租户", currentTenant.Name);
    }

    /// <summary>
    /// 属性每次读取都穿透到访问器，不做本地缓存
    /// </summary>
    /// <remarks>
    /// 缓存会让「中间件晚于某个作用域解析出租户」的场景读到陈旧值，这里用直接改写访问器来反证没有缓存。
    /// </remarks>
    [Fact]
    public void Id_ReadsThroughAccessorOnEveryAccess()
    {
        var accessor = new FakeCurrentTenantAccessor();
        var currentTenant = new CurrentTenant(accessor);

        Assert.Null(currentTenant.Id);

        accessor.Current = new BasicTenantInfo(42L, "租户四二");

        Assert.Equal<long?>(42L, currentTenant.Id);
        Assert.Equal("租户四二", currentTenant.Name);

        accessor.Current = null;

        Assert.Null(currentTenant.Id);
    }

    /// <summary>
    /// 每次切换返回互不相同的释放器实例
    /// </summary>
    [Fact]
    public void Change_CalledTwice_ReturnsDistinctDisposables()
    {
        var currentTenant = CreateWithFakeAccessor();

        var outer = currentTenant.Change(1L);
        var inner = currentTenant.Change(2L);

        Assert.NotSame(outer, inner);

        inner.Dispose();
        Assert.Equal<long?>(1L, currentTenant.Id);

        outer.Dispose();
        Assert.Null(currentTenant.Id);
    }

    /// <summary>
    /// 实现了当前租户契约并被标记为瞬时依赖
    /// </summary>
    /// <remarks>
    /// 瞬时生命周期是这个类型的注册前提：它本身无状态，状态全在访问器里，一旦被误改成单例并不会立刻报错，
    /// 但会让「按容器作用域取当前租户」的假设失效，所以把标记接口一并钉住。
    /// </remarks>
    [Fact]
    public void CurrentTenant_ImplementsContractAndTransientLifetimeMarker()
    {
        var currentTenant = CreateWithFakeAccessor();

        Assert.IsAssignableFrom<ICurrentTenant>(currentTenant);
        Assert.IsAssignableFrom<ITransientDependency>(currentTenant);
    }

    /// <summary>
    /// 搭配 AsyncLocal 访问器时租户上下文跨 await 继续生效
    /// </summary>
    /// <remarks>
    /// 这是 AsyncLocal 访问器存在的全部理由：作用域必须能穿过 Task.Yield、Task.Delay 以及派生任务。
    /// 所有写入都关在 Task.Run 的独立执行上下文里，避免污染同集合内的其他用例。
    /// </remarks>
    [Fact]
    public async Task Change_AcrossAwait_FlowsIntoContinuations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var observed = await Task.Run(async () =>
        {
            var currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);

            using (currentTenant.Change(7L, "租户七"))
            {
                await Task.Yield();
                var afterYield = currentTenant.Id;

                await Task.Delay(1, cancellationToken);
                var afterDelay = currentTenant.Id;

                var inNestedTask = await Task.Run(() => currentTenant.Id, cancellationToken);

                return (afterYield, afterDelay, inNestedTask);
            }
        }, cancellationToken);

        Assert.Equal<long?>(7L, observed.afterYield);
        Assert.Equal<long?>(7L, observed.afterDelay);
        Assert.Equal<long?>(7L, observed.inNestedTask);
        Assert.Null(AsyncLocalCurrentTenantAccessor.Instance.Current);
    }

    /// <summary>
    /// 子任务内的切换不会回流到调用方的执行上下文
    /// </summary>
    /// <remarks>
    /// AsyncLocal 的写时复制语义：子流程写入只影响自己这一支。
    /// 用例刻意不释放作用域，把「隔离」和「释放器还原」两件事分开验证——
    /// 如果隔离不成立，即使释放器完全正确，调用方也会读到子任务遗留的租户。
    /// </remarks>
    [Fact]
    public async Task Change_InsideChildTask_DoesNotLeakToCallerContext()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);

        var observedInChild = await Task.Run(() =>
        {
            _ = currentTenant.Change(11L, "租户十一");
            return currentTenant.Id;
        }, cancellationToken);

        Assert.Equal<long?>(11L, observedInChild);
        Assert.Null(currentTenant.Id);
    }

    /// <summary>
    /// 并行流程各自持有互不干扰的租户上下文
    /// </summary>
    /// <remarks>
    /// 带整体超时保护，任何一支卡住都会在 30 秒内失败而不是让 CI 挂死。
    /// </remarks>
    [Fact]
    public async Task Change_InParallelFlows_AreIsolatedFromEachOther()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);

        async Task<long?> ObserveInIsolatedFlowAsync(long tenantId)
        {
            return await Task.Run(async () =>
            {
                using (currentTenant.Change(tenantId, $"租户{tenantId}"))
                {
                    await Task.Delay(5, cancellationToken);
                    return currentTenant.Id;
                }
            }, cancellationToken);
        }

        var tasks = Enumerable.Range(1, 8).Select(index => ObserveInIsolatedFlowAsync(index)).ToArray();
        var observed = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal(8, observed.Length);
        for (var index = 0; index < observed.Length; index++)
        {
            Assert.Equal<long?>(index + 1, observed[index]);
        }

        Assert.Null(currentTenant.Id);
    }

    /// <summary>
    /// 创建基于普通字段访问器的当前租户实例
    /// </summary>
    /// <returns>当前租户</returns>
    private static CurrentTenant CreateWithFakeAccessor()
    {
        return new CurrentTenant(new FakeCurrentTenantAccessor());
    }
}

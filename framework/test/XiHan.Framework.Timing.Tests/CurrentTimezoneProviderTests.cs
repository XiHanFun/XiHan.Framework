// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 当前时区提供器测试
/// </summary>
/// <remarks>
/// 该实现以静态 AsyncLocal 承载时区，语义上等价于「按执行上下文隔离的环境变量」：
/// 值归属于异步流而不是实例，同一流里任何实例读到的都是同一个值；
/// 父流程的赋值向下可见，子流程的赋值不回流，并行流程互不干扰。
/// 这套语义决定了它能否安全地按请求承载时区，因此在这里逐条锁死。
/// </remarks>
public class CurrentTimezoneProviderTests
{
    private const string ShanghaiTimeZone = "Asia/Shanghai";
    private const string TokyoTimeZone = "Asia/Tokyo";

    /// <summary>
    /// 当前流程未赋值时没有任何时区
    /// </summary>
    [Fact]
    public void TimeZone_WhenCurrentFlowHasNotAssigned_IsNull()
    {
        var provider = new CurrentTimezoneProvider();

        Assert.Null(provider.TimeZone);
    }

    /// <summary>
    /// 赋值后可原样读回
    /// </summary>
    [Fact]
    public void TimeZone_AfterSet_ReturnsAssignedValue()
    {
        var provider = new CurrentTimezoneProvider
        {
            TimeZone = ShanghaiTimeZone
        };

        Assert.Equal(ShanghaiTimeZone, provider.TimeZone);
    }

    /// <summary>
    /// 置空可清除已设置的时区
    /// </summary>
    [Fact]
    public void TimeZone_SetToNull_ClearsPreviousValue()
    {
        var provider = new CurrentTimezoneProvider
        {
            TimeZone = ShanghaiTimeZone
        };

        provider.TimeZone = null;

        Assert.Null(provider.TimeZone);
    }

    /// <summary>
    /// 空白字符串按原样保存，不做归一化
    /// </summary>
    /// <remarks>
    /// 时钟侧是用 IsNullOrWhiteSpace 判空的，提供器本身不负责清洗，这里锁死职责边界。
    /// </remarks>
    [Fact]
    public void TimeZone_SetToWhiteSpace_IsStoredAsIs()
    {
        var provider = new CurrentTimezoneProvider
        {
            TimeZone = "   "
        };

        Assert.Equal("   ", provider.TimeZone);
    }

    /// <summary>
    /// 同一流程内不同实例读写的是同一个时区
    /// </summary>
    /// <remarks>
    /// 业务代码与时钟从容器拿到的未必是同一个实例（替换注册、手动构造），
    /// 时区若按实例隔离，时钟就读不到业务侧的赋值；隔离边界是异步流，由下面的并行流程用例锁死。
    /// </remarks>
    [Fact]
    public void TimeZone_AcrossInstancesInSameFlow_SharesAssignedValue()
    {
        var first = new CurrentTimezoneProvider
        {
            TimeZone = ShanghaiTimeZone
        };
        var second = new CurrentTimezoneProvider();

        Assert.Equal(ShanghaiTimeZone, second.TimeZone);

        second.TimeZone = TokyoTimeZone;

        Assert.Equal(TokyoTimeZone, first.TimeZone);
        Assert.Equal(TokyoTimeZone, second.TimeZone);
    }

    /// <summary>
    /// 被 await 的异步方法内的赋值对其下游可见，返回后调用方恢复为自己的时区
    /// </summary>
    /// <remarks>
    /// 中间件正是这种形状：在自身异步方法里写时区再 await 下游，请求结束后时区不会留给调用方。
    /// 写入与读取故意使用不同实例，同时证明值跟随异步流而非实例。
    /// </remarks>
    [Fact]
    public async Task TimeZone_AssignedInsideAwaitedAsyncMethod_IsVisibleDownstreamAndRestoredForCaller()
    {
        var provider = new CurrentTimezoneProvider
        {
            TimeZone = ShanghaiTimeZone
        };
        string? observedDownstream = null;

        await AssignThenInvokeDownstreamAsync(
            new CurrentTimezoneProvider(),
            TokyoTimeZone,
            () => observedDownstream = provider.TimeZone);

        Assert.Equal(TokyoTimeZone, observedDownstream);
        Assert.Equal(ShanghaiTimeZone, provider.TimeZone);
    }

    /// <summary>
    /// 父流程设置的时区对子流程可见
    /// </summary>
    [Fact]
    public async Task TimeZone_SetBeforeChildFlow_IsVisibleInsideChildFlow()
    {
        var provider = new CurrentTimezoneProvider
        {
            TimeZone = ShanghaiTimeZone
        };

        var observed = await Task.Run(() => provider.TimeZone, TestContext.Current.CancellationToken);

        Assert.Equal(ShanghaiTimeZone, observed);
    }

    /// <summary>
    /// 子流程内的赋值不会回流到父流程
    /// </summary>
    [Fact]
    public async Task TimeZone_SetInsideChildFlow_DoesNotLeakBackToParent()
    {
        var provider = new CurrentTimezoneProvider
        {
            TimeZone = ShanghaiTimeZone
        };

        var observedAfterOverwrite = await Task.Run(
            () =>
            {
                provider.TimeZone = TokyoTimeZone;
                return provider.TimeZone;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(TokyoTimeZone, observedAfterOverwrite);
        Assert.Equal(ShanghaiTimeZone, provider.TimeZone);
    }

    /// <summary>
    /// 并行流程各自持有独立的时区副本
    /// </summary>
    [Fact]
    public async Task TimeZone_AcrossParallelFlows_DoesNotBleedBetweenFlows()
    {
        var provider = new CurrentTimezoneProvider();
        var token = TestContext.Current.CancellationToken;

        var first = Task.Run(
            () =>
            {
                provider.TimeZone = ShanghaiTimeZone;
                return provider.TimeZone;
            },
            token);
        var second = Task.Run(
            () =>
            {
                provider.TimeZone = TokyoTimeZone;
                return provider.TimeZone;
            },
            token);

        var firstResult = await first;
        var secondResult = await second;

        Assert.Equal(ShanghaiTimeZone, firstResult);
        Assert.Equal(TokyoTimeZone, secondResult);
        Assert.Null(provider.TimeZone);
    }

    /// <summary>
    /// 以中间件的形状写入时区，再异步执行下游
    /// </summary>
    /// <param name="provider">写入时区所用的提供器</param>
    /// <param name="timeZone">时区</param>
    /// <param name="downstream">下游逻辑</param>
    private static async Task AssignThenInvokeDownstreamAsync(
        ICurrentTimezoneProvider provider,
        string timeZone,
        Action downstream)
    {
        provider.TimeZone = timeZone;
        await Task.Yield();
        downstream();
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 当前时区提供器测试
/// </summary>
/// <remarks>
/// 该实现以 AsyncLocal 承载时区，语义上等价于「按执行上下文隔离的环境变量」：
/// 父流程的赋值向下可见，子流程的赋值不回流。这套语义决定了它能否安全地按请求承载时区，
/// 因此在这里连同实例隔离一起锁死。
/// </remarks>
public class CurrentTimezoneProviderTests
{
    private const string ShanghaiTimeZone = "Asia/Shanghai";
    private const string TokyoTimeZone = "Asia/Tokyo";

    /// <summary>
    /// 新建实例时没有任何时区
    /// </summary>
    [Fact]
    public void TimeZone_OnNewInstance_IsNull()
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
    /// 不同实例之间互不干扰
    /// </summary>
    [Fact]
    public void TimeZone_AcrossInstances_IsIsolated()
    {
        var first = new CurrentTimezoneProvider
        {
            TimeZone = ShanghaiTimeZone
        };
        var second = new CurrentTimezoneProvider();

        second.TimeZone = TokyoTimeZone;

        Assert.Equal(ShanghaiTimeZone, first.TimeZone);
        Assert.Equal(TokyoTimeZone, second.TimeZone);
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
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 动态服务配置测试
/// </summary>
/// <remarks>
/// 这是运行期唯一能改后台服务行为的入口，契约有三层：
/// 初值来自静态选项、非法值直接拒绝（而不是悄悄夹逼到合法区间）、
/// 只有"值真的变了"才广播事件（否则订阅方会被重复通知刷屏）。
/// 类型自称线程安全，因此额外补一个多线程读写用例，断言读到的永远是某次完整写入的值而非撕裂值。
/// </remarks>
public class DynamicServiceConfigTests
{
    /// <summary>
    /// 兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 选项为 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DynamicServiceConfig(null!));
    }

    /// <summary>
    /// 初值取自静态配置，任务处理默认启用
    /// </summary>
    [Fact]
    public void Constructor_TakesInitialValuesFromOptions()
    {
        var config = CreateConfig(maxConcurrentTasks: 9, idleDelayMilliseconds: 250);

        Assert.Equal(9, config.MaxConcurrentTasks);
        Assert.Equal(250, config.IdleDelayMilliseconds);
        Assert.True(config.IsTaskProcessingEnabled);
    }

    /// <summary>
    /// 调整并发数后立即生效并广播变更
    /// </summary>
    [Fact]
    public void UpdateMaxConcurrentTasks_WhenValueChanges_RaisesConfigChanged()
    {
        var config = CreateConfig(maxConcurrentTasks: 2);
        var events = SubscribeEvents(config);

        config.UpdateMaxConcurrentTasks(6);

        Assert.Equal(6, config.MaxConcurrentTasks);
        var changed = Assert.Single(events);
        Assert.Equal(nameof(IDynamicServiceConfig.MaxConcurrentTasks), changed.PropertyName);
        Assert.Equal(2, changed.OldValue);
        Assert.Equal(6, changed.NewValue);
    }

    /// <summary>
    /// 并发数写入相同值时不广播事件
    /// </summary>
    [Fact]
    public void UpdateMaxConcurrentTasks_WhenValueUnchanged_DoesNotRaiseEvent()
    {
        var config = CreateConfig(maxConcurrentTasks: 4);
        var events = SubscribeEvents(config);

        config.UpdateMaxConcurrentTasks(4);

        Assert.Empty(events);
    }

    /// <summary>
    /// 并发数必须大于 0，非法值直接拒绝且不改变现值
    /// </summary>
    /// <param name="invalid">非法并发数</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void UpdateMaxConcurrentTasks_WhenNotPositive_ThrowsAndKeepsCurrentValue(int invalid)
    {
        var config = CreateConfig(maxConcurrentTasks: 3);
        var events = SubscribeEvents(config);

        var exception = Assert.Throws<ArgumentException>(() => config.UpdateMaxConcurrentTasks(invalid));

        Assert.Equal("maxConcurrentTasks", exception.ParamName);
        Assert.Equal(3, config.MaxConcurrentTasks);
        Assert.Empty(events);
    }

    /// <summary>
    /// 调整空闲延迟后立即生效并广播变更
    /// </summary>
    [Fact]
    public void UpdateIdleDelay_WhenValueChanges_RaisesConfigChanged()
    {
        var config = CreateConfig(idleDelayMilliseconds: 1000);
        var events = SubscribeEvents(config);

        config.UpdateIdleDelay(100);

        Assert.Equal(100, config.IdleDelayMilliseconds);
        var changed = Assert.Single(events);
        Assert.Equal(nameof(IDynamicServiceConfig.IdleDelayMilliseconds), changed.PropertyName);
        Assert.Equal(1000, changed.OldValue);
        Assert.Equal(100, changed.NewValue);
    }

    /// <summary>
    /// 空闲延迟允许为 0（表示不额外等待）
    /// </summary>
    [Fact]
    public void UpdateIdleDelay_WhenZero_IsAccepted()
    {
        var config = CreateConfig(idleDelayMilliseconds: 500);

        config.UpdateIdleDelay(0);

        Assert.Equal(0, config.IdleDelayMilliseconds);
    }

    /// <summary>
    /// 空闲延迟不能为负数
    /// </summary>
    [Fact]
    public void UpdateIdleDelay_WhenNegative_ThrowsAndKeepsCurrentValue()
    {
        var config = CreateConfig(idleDelayMilliseconds: 800);

        var exception = Assert.Throws<ArgumentException>(() => config.UpdateIdleDelay(-1));

        Assert.Equal("idleDelayMilliseconds", exception.ParamName);
        Assert.Equal(800, config.IdleDelayMilliseconds);
    }

    /// <summary>
    /// 暂停与恢复任务处理各广播一次事件
    /// </summary>
    [Fact]
    public void SetTaskProcessingEnabled_TogglesAndRaisesConfigChanged()
    {
        var config = CreateConfig();
        var events = SubscribeEvents(config);

        config.SetTaskProcessingEnabled(false);
        config.SetTaskProcessingEnabled(true);

        Assert.True(config.IsTaskProcessingEnabled);
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(nameof(IDynamicServiceConfig.IsTaskProcessingEnabled), e.PropertyName));
        Assert.True(events[0].OldValue is bool first && first);
        Assert.True(events[1].NewValue is bool second && second);
    }

    /// <summary>
    /// 重复写入相同开关值不广播事件
    /// </summary>
    [Fact]
    public void SetTaskProcessingEnabled_WhenValueUnchanged_DoesNotRaiseEvent()
    {
        var config = CreateConfig();
        var events = SubscribeEvents(config);

        config.SetTaskProcessingEnabled(true);

        Assert.True(config.IsTaskProcessingEnabled);
        Assert.Empty(events);
    }

    /// <summary>
    /// 退订后不再收到变更通知
    /// </summary>
    [Fact]
    public void ConfigChanged_AfterUnsubscribe_IsNotInvoked()
    {
        var config = CreateConfig(maxConcurrentTasks: 1);
        var count = 0;

        void Handler(object? sender, ConfigChangedEventArgs e) => count++;

        config.ConfigChanged += Handler;
        config.UpdateMaxConcurrentTasks(2);
        config.ConfigChanged -= Handler;
        config.UpdateMaxConcurrentTasks(3);

        Assert.Equal(1, count);
    }

    /// <summary>
    /// 多线程并发写入时，任意时刻读到的都是某次完整写入的值，不会出现中间态
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task UpdateIdleDelay_UnderConcurrency_NeverExposesTornValue()
    {
        var config = CreateConfig(idleDelayMilliseconds: 1000);
        var allowedValues = new HashSet<int> { 1000, 11, 22, 33, 44, 55, 66, 77 };
        var writers = new[] { 11, 22, 33, 44, 55, 66, 77 };
        var observed = new System.Collections.Concurrent.ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            writers,
            TestContext.Current.CancellationToken,
            (value, _) =>
            {
                for (var i = 0; i < 500; i++)
                {
                    config.UpdateIdleDelay(value);
                    observed.Add(config.IdleDelayMilliseconds);
                }

                return ValueTask.CompletedTask;
            });

        Assert.All(observed, value => Assert.True(allowedValues.Contains(value), $"读到了非法的中间值：{value}"));
        Assert.Contains(config.IdleDelayMilliseconds, allowedValues);
    }

    /// <summary>
    /// 创建动态配置
    /// </summary>
    /// <param name="maxConcurrentTasks">最大并发任务数</param>
    /// <param name="idleDelayMilliseconds">空闲延迟时间</param>
    /// <returns>动态配置</returns>
    private static DynamicServiceConfig CreateConfig(int maxConcurrentTasks = 5, int idleDelayMilliseconds = 1000)
    {
        var options = new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = maxConcurrentTasks,
            IdleDelayMilliseconds = idleDelayMilliseconds
        };

        return new DynamicServiceConfig(Microsoft.Extensions.Options.Options.Create(options));
    }

    /// <summary>
    /// 订阅配置变更事件并收集事件参数
    /// </summary>
    /// <param name="config">动态配置</param>
    /// <returns>事件参数列表</returns>
    private static List<ConfigChangedEventArgs> SubscribeEvents(DynamicServiceConfig config)
    {
        var events = new List<ConfigChangedEventArgs>();
        config.ConfigChanged += (_, e) => events.Add(e);
        return events;
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 后台服务配置选项测试
/// </summary>
/// <remarks>
/// 该选项没有校验方法，默认值即契约。其中两处默认值组合最容易被误读：
/// 重试默认开启（所以基类会自建指数退避策略），而超时控制默认关闭且超时时间为 0，
/// 也就是说只把 EnableTaskTimeout 打开而不设时间是不会生效的——这条组合语义单列一个用例锁住。
/// </remarks>
public class XiHanBackgroundServiceOptionsTests
{
    /// <summary>
    /// 并发与空闲相关默认值
    /// </summary>
    [Fact]
    public void Defaults_ConcurrencyAndIdle()
    {
        var options = new XiHanBackgroundServiceOptions();

        Assert.Equal(5, options.MaxConcurrentTasks);
        Assert.Equal(1000, options.IdleDelayMilliseconds);
    }

    /// <summary>
    /// 重试默认开启，重试次数与间隔有默认值
    /// </summary>
    [Fact]
    public void Defaults_RetryIsEnabled()
    {
        var options = new XiHanBackgroundServiceOptions();

        Assert.True(options.EnableRetry);
        Assert.Equal(3, options.MaxRetryCount);
        Assert.Equal(5000, options.RetryDelayMilliseconds);
    }

    /// <summary>
    /// 超时控制默认关闭，且超时时间默认为 0（表示不超时）
    /// </summary>
    [Fact]
    public void Defaults_TaskTimeoutIsDisabled()
    {
        var options = new XiHanBackgroundServiceOptions();

        Assert.False(options.EnableTaskTimeout);
        Assert.Equal(0, options.TaskTimeoutMilliseconds);
    }

    /// <summary>
    /// 停止时等待任务收尾的默认超时为 30 秒
    /// </summary>
    [Fact]
    public void Defaults_ShutdownTimeoutIsThirtySeconds()
    {
        var options = new XiHanBackgroundServiceOptions();

        Assert.Equal(30000, options.ShutdownTimeoutMilliseconds);
        Assert.Equal(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(options.ShutdownTimeoutMilliseconds));
    }

    /// <summary>
    /// 全部选项可写，支持配置绑定覆盖
    /// </summary>
    [Fact]
    public void Properties_AreWritable()
    {
        var options = new XiHanBackgroundServiceOptions
        {
            MaxConcurrentTasks = 12,
            IdleDelayMilliseconds = 20,
            EnableRetry = false,
            EnableTaskTimeout = true,
            MaxRetryCount = 7,
            RetryDelayMilliseconds = 30,
            TaskTimeoutMilliseconds = 40,
            ShutdownTimeoutMilliseconds = 50
        };

        Assert.Equal(12, options.MaxConcurrentTasks);
        Assert.Equal(20, options.IdleDelayMilliseconds);
        Assert.False(options.EnableRetry);
        Assert.True(options.EnableTaskTimeout);
        Assert.Equal(7, options.MaxRetryCount);
        Assert.Equal(30, options.RetryDelayMilliseconds);
        Assert.Equal(40, options.TaskTimeoutMilliseconds);
        Assert.Equal(50, options.ShutdownTimeoutMilliseconds);
    }
}

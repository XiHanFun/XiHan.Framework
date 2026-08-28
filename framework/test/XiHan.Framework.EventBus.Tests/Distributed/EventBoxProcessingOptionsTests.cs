// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Distributed;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 事件盒后台处理配置测试
/// </summary>
/// <remarks>
/// 配置节名与默认值是运维口径的一部分：节名变了线上配置会静默失效，
/// 默认值变了会直接改变轮询压力与重试语义，因此逐项锁死。
/// </remarks>
public class EventBoxProcessingOptionsTests
{
    /// <summary>
    /// 配置节名保持稳定，否则已部署的配置文件会静默失效
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:EventBus:EventBoxes", EventBoxProcessingOptions.SectionName);
    }

    /// <summary>
    /// 默认轮询间隔为两秒
    /// </summary>
    [Fact]
    public void PollingIntervalMilliseconds_DefaultsToTwoSeconds()
    {
        Assert.Equal(2000, new EventBoxProcessingOptions().PollingIntervalMilliseconds);
    }

    /// <summary>
    /// 收发件箱默认批量大小一致
    /// </summary>
    [Fact]
    public void BatchSizes_DefaultToOneHundred()
    {
        var options = new EventBoxProcessingOptions();

        Assert.Equal(100, options.OutboxBatchSize);
        Assert.Equal(100, options.InboxBatchSize);
    }

    /// <summary>
    /// 默认重试策略为最多五次、每次间隔十秒
    /// </summary>
    [Fact]
    public void RetryPolicy_HasExpectedDefaults()
    {
        var options = new EventBoxProcessingOptions();

        Assert.Equal(5, options.MaxInboxRetryCount);
        Assert.Equal(10, options.InboxRetryDelaySeconds);
    }

    /// <summary>
    /// 各项配置均可被覆盖
    /// </summary>
    [Fact]
    public void Options_AreMutable()
    {
        var options = new EventBoxProcessingOptions
        {
            PollingIntervalMilliseconds = 500,
            OutboxBatchSize = 10,
            InboxBatchSize = 20,
            MaxInboxRetryCount = 3,
            InboxRetryDelaySeconds = 1
        };

        Assert.Equal(500, options.PollingIntervalMilliseconds);
        Assert.Equal(10, options.OutboxBatchSize);
        Assert.Equal(20, options.InboxBatchSize);
        Assert.Equal(3, options.MaxInboxRetryCount);
        Assert.Equal(1, options.InboxRetryDelaySeconds);
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 事件盒后台处理配置
/// </summary>
public class EventBoxProcessingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:EventBus:EventBoxes";

    /// <summary>
    /// 轮询间隔（毫秒）
    /// </summary>
    public int PollingIntervalMilliseconds { get; set; } = 2000;

    /// <summary>
    /// 发件箱单批处理数量
    /// </summary>
    public int OutboxBatchSize { get; set; } = 100;

    /// <summary>
    /// 收件箱单批处理数量
    /// </summary>
    public int InboxBatchSize { get; set; } = 100;

    /// <summary>
    /// 收件箱最大重试次数
    /// </summary>
    public int MaxInboxRetryCount { get; set; } = 5;

    /// <summary>
    /// 收件箱重试延迟秒数
    /// </summary>
    public int InboxRetryDelaySeconds { get; set; } = 10;
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventBoxProcessingOptions
// Guid:5c22bb86-76fb-4d3d-86df-4d97af50331d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 20:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

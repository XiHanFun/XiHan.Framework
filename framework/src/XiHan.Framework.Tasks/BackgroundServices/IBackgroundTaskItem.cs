// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundServices;

/// <summary>
/// 后台任务项接口
/// </summary>
public interface IBackgroundTaskItem
{
    /// <summary>
    /// 任务唯一标识
    /// </summary>
    string TaskId { get; }

    /// <summary>
    /// 任务数据
    /// </summary>
    object? Data { get; }

    /// <summary>
    /// 任务创建时间
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 已重试次数
    /// </summary>
    int RetryCount { get; set; }
}

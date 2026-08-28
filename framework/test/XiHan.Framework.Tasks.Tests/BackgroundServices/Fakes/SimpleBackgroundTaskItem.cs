// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices.Fakes;

/// <summary>
/// 最小后台任务项
/// </summary>
/// <remarks>
/// 只带一个可控的任务标识，便于按标识断言处理顺序与去重行为；
/// 与既有示例里的邮件任务项不同，这里刻意不带业务字段，避免把业务语义混进基类行为验证。
/// </remarks>
public sealed class SimpleBackgroundTaskItem : IBackgroundTaskItem
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="taskId">任务唯一标识</param>
    /// <param name="data">任务数据</param>
    public SimpleBackgroundTaskItem(string taskId, object? data = null)
    {
        TaskId = taskId;
        Data = data;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 任务唯一标识
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 任务数据
    /// </summary>
    public object? Data { get; }

    /// <summary>
    /// 任务创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 已重试次数
    /// </summary>
    public int RetryCount { get; set; }
}

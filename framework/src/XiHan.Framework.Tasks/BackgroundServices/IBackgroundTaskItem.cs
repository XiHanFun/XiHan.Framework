#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBackgroundTaskItem
// Guid:${guid}
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/17 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

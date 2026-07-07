#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBackgroundJobManager
// Guid:107ad344-2ac7-46f9-998f-f1e33b707814
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.BackgroundJobs.Models;

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业管理器（fire-and-forget 一次性异步作业的统一入队门面）
/// </summary>
/// <remarks>
/// 入队仅把作业持久化到 <see cref="IBackgroundJobStore"/> 并立即返回，执行完全由后台 Worker 轮询驱动，
/// 因此天然具备"持久化 + 崩溃恢复 + 失败退避重试"能力，与业务请求解耦。
/// </remarks>
public interface IBackgroundJobManager
{
    /// <summary>
    /// 入队一个后台作业
    /// </summary>
    /// <typeparam name="TArgs">作业参数类型（决定由哪个处理器执行）</typeparam>
    /// <param name="args">作业参数</param>
    /// <param name="priority">优先级</param>
    /// <param name="delay">首次执行前的延迟（为空表示尽快执行）</param>
    /// <returns>作业唯一标识</returns>
    Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null);
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IJobScheduler.cs
// Guid:1e4fa96d-d4b8-4167-a896-4ad58e363927
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:45:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务调度器接口
/// </summary>
public interface IJobScheduler
{
    /// <summary>
    /// 注册任务
    /// </summary>
    /// <param name="jobInfo">任务信息</param>
    void RegisterJob(JobInfo jobInfo);

    /// <summary>
    /// 取消注册任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    void UnregisterJob(string jobName);

    /// <summary>
    /// 暂停任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    void PauseJob(string jobName);

    /// <summary>
    /// 恢复任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    void ResumeJob(string jobName);

    /// <summary>
    /// 手动触发任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="parameters">参数</param>
    /// <returns>任务实例ID</returns>
    Task<string> TriggerJobAsync(string jobName, IDictionary<string, object?>? parameters = null);

    /// <summary>
    /// 获取下次执行时间
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <returns>下次执行时间</returns>
    DateTimeOffset? GetNextFireTime(string jobName);

    /// <summary>
    /// 获取所有已注册的任务信息
    /// </summary>
    /// <returns>任务信息列表</returns>
    IReadOnlyList<JobInfo> GetAllJobs();

    /// <summary>
    /// 启动调度器
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止调度器
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

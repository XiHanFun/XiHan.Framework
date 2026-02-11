#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IJobStore
// Guid:8726538f-4902-4e12-b4ec-c3e711db4f31
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 16:08:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

/// <summary>
/// 任务存储接口
/// </summary>
public interface IJobStore
{
    /// <summary>
    /// 保存任务实例
    /// </summary>
    /// <param name="jobInstance">任务实例</param>
    Task SaveJobInstanceAsync(JobInstance jobInstance);

    /// <summary>
    /// 更新任务实例状态
    /// </summary>
    /// <param name="instanceId">实例唯一标识</param>
    /// <param name="status">状态</param>
    Task UpdateJobStatusAsync(string instanceId, JobStatus status);

    /// <summary>
    /// 保存任务执行历史
    /// </summary>
    /// <param name="history">执行历史</param>
    Task SaveJobHistoryAsync(JobHistory history);

    /// <summary>
    /// 获取任务实例
    /// </summary>
    /// <param name="instanceId">实例唯一标识</param>
    /// <returns>任务实例</returns>
    Task<JobInstance?> GetJobInstanceAsync(string instanceId);

    /// <summary>
    /// 获取任务执行历史
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">页大小</param>
    /// <returns>执行历史列表</returns>
    Task<IReadOnlyList<JobHistory>> GetJobHistoryAsync(string jobName, int pageIndex = 1, int pageSize = 20);

    /// <summary>
    /// 获取运行中的任务实例
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <returns>运行中的任务实例列表</returns>
    Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName);

    /// <summary>
    /// 清理过期的历史记录
    /// </summary>
    /// <param name="retentionDays">保留天数</param>
    Task CleanupHistoryAsync(int retentionDays);
}

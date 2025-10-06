#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IScheduledJobManager
// Guid:4e5f6a7b-8c9d-0e1f-2a3b-4c5d6e7f8a9b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:33:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Quartz;

namespace XiHan.Framework.Tasks.ScheduledJobs;

/// <summary>
/// 调度任务管理器接口
/// </summary>
public interface IScheduledJobManager
{
    /// <summary>
    /// 添加调度任务
    /// </summary>
    /// <typeparam name="TJob">任务类型</typeparam>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <param name="cronExpression">Cron 表达式</param>
    /// <param name="description">任务描述</param>
    /// <param name="startImmediately">是否立即启动</param>
    /// <param name="priority">优先级</param>
    /// <returns>是否添加成功</returns>
    Task<bool> AddJobAsync<TJob>(
        string jobName,
        string jobGroup,
        string cronExpression,
        string? description = null,
        bool startImmediately = false,
        int priority = 5) where TJob : IJob;

    /// <summary>
    /// 删除调度任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <returns>是否删除成功</returns>
    Task<bool> RemoveJobAsync(string jobName, string jobGroup);

    /// <summary>
    /// 暂停调度任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <returns>是否暂停成功</returns>
    Task<bool> PauseJobAsync(string jobName, string jobGroup);

    /// <summary>
    /// 恢复调度任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <returns>是否恢复成功</returns>
    Task<bool> ResumeJobAsync(string jobName, string jobGroup);

    /// <summary>
    /// 立即执行调度任务
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <returns>是否执行成功</returns>
    Task<bool> TriggerJobAsync(string jobName, string jobGroup);

    /// <summary>
    /// 更新任务的 Cron 表达式
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <param name="cronExpression">新的 Cron 表达式</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateJobCronAsync(string jobName, string jobGroup, string cronExpression);

    /// <summary>
    /// 获取所有调度任务信息
    /// </summary>
    /// <returns>任务信息列表</returns>
    Task<List<XiHanScheduledJobInfo>> GetAllJobsAsync();

    /// <summary>
    /// 获取指定任务信息
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <returns>任务信息</returns>
    Task<XiHanScheduledJobInfo?> GetJobInfoAsync(string jobName, string jobGroup);

    /// <summary>
    /// 检查任务是否存在
    /// </summary>
    /// <param name="jobName">任务名称</param>
    /// <param name="jobGroup">任务分组</param>
    /// <returns>是否存在</returns>
    Task<bool> JobExistsAsync(string jobName, string jobGroup);

    /// <summary>
    /// 启动调度器
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 停止调度器
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 获取调度器状态
    /// </summary>
    /// <returns>是否运行中</returns>
    Task<bool> IsRunningAsync();
}

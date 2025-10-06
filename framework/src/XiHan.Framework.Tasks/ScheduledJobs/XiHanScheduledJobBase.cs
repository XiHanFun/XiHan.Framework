#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanScheduledJobBase
// Guid:849e8610-0a52-4da8-aba4-ac1e71724502
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/17 15:00:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Quartz;

namespace XiHan.Framework.Tasks.ScheduledJobs;

/// <summary>
/// 调度任务基类
/// 提供统一的任务执行框架和异常处理
/// </summary>
public abstract class XiHanScheduledJobBase : IJob
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    protected XiHanScheduledJobBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Quartz 任务执行入口
    /// </summary>
    /// <param name="context">任务执行上下文</param>
    public async Task Execute(IJobExecutionContext context)
    {
        var jobName = context.JobDetail.Key.Name;
        var jobGroup = context.JobDetail.Key.Group;
        var startTime = DateTimeOffset.UtcNow;

        Logger.LogInformation("调度任务开始执行: {JobGroup}.{JobName}", jobGroup, jobName);

        try
        {
            // 调用子类实现的具体任务逻辑
            await ExecuteJobAsync(context);

            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            Logger.LogInformation("调度任务执行成功: {JobGroup}.{JobName}, 耗时: {Duration}ms", jobGroup, jobName, duration);
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            Logger.LogError(ex, "调度任务执行失败: {JobGroup}.{JobName}, 耗时: {Duration}ms", jobGroup, jobName, duration);

            // 调用失败处理方法
            await OnExecuteFailedAsync(context, ex);

            throw;
        }
    }

    /// <summary>
    /// 子类实现的具体任务逻辑
    /// </summary>
    /// <param name="context">任务执行上下文</param>
    protected abstract Task ExecuteJobAsync(IJobExecutionContext context);

    /// <summary>
    /// 任务执行失败时的处理（可选实现）
    /// </summary>
    /// <param name="context">任务执行上下文</param>
    /// <param name="exception">异常信息</param>
    protected virtual Task OnExecuteFailedAsync(IJobExecutionContext context, Exception exception)
    {
        return Task.CompletedTask;
    }
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SampleScheduledJob
// Guid:8c9d0e1f-2a3b-4c5d-6e7f-8a9b0c1d2e3f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Quartz;
using XiHan.Framework.Tasks.ScheduledJobs;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs;

/// <summary>
/// 示例调度任务
/// 这是一个简单的示例，演示如何创建调度任务
/// </summary>
[ScheduledJob(
    JobName = "SampleJob",
    JobGroup = "Examples",
    CronExpression = "0 */5 * * * ?",  // 每5分钟执行一次
    Description = "这是一个示例调度任务",
    AutoStart = false,  // 默认不自动启动，需要手动启动
    Priority = 5)]
public class SampleScheduledJob : XiHanScheduledJobBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public SampleScheduledJob(ILogger<SampleScheduledJob> logger) : base(logger)
    {
    }

    /// <summary>
    /// 执行任务逻辑
    /// </summary>
    /// <param name="context">任务执行上下文</param>
    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        Logger.LogInformation("示例任务开始执行，执行时间: {FireTime}", context.FireTimeUtc);

        try
        {
            // 模拟业务逻辑
            await Task.Delay(100);

            Logger.LogInformation("示例任务执行成功");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "示例任务执行过程中发生错误");
            throw;
        }
    }

    /// <summary>
    /// 任务失败时的处理
    /// </summary>
    /// <param name="context">任务执行上下文</param>
    /// <param name="exception">异常信息</param>
    protected override Task OnExecuteFailedAsync(IJobExecutionContext context, Exception exception)
    {
        Logger.LogError(exception, "示例任务执行失败，可以在这里添加告警、重试等逻辑");
        return Task.CompletedTask;
    }
}

/// <summary>
/// 每日报表任务示例
/// </summary>
[ScheduledJob(
    JobName = "DailyReportJob",
    JobGroup = "Reports",
    CronExpression = "0 0 9 * * ?",  // 每天早上9点执行
    Description = "生成每日报表",
    AutoStart = false,
    Priority = 8)]
public class DailyReportJob : XiHanScheduledJobBase
{
    public DailyReportJob(ILogger<DailyReportJob> logger) : base(logger)
    {
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        Logger.LogInformation("开始生成每日报表...");

        // 实现报表生成逻辑
        await Task.Delay(1000);

        Logger.LogInformation("每日报表生成完成");
    }
}

/// <summary>
/// 数据清理任务示例
/// </summary>
[ScheduledJob(
    JobName = "DataCleanupJob",
    JobGroup = "Maintenance",
    CronExpression = "0 0 2 * * ?",  // 每天凌晨2点执行
    Description = "清理过期数据",
    AutoStart = false,
    Priority = 3)]
public class DataCleanupJob : XiHanScheduledJobBase
{
    public DataCleanupJob(ILogger<DataCleanupJob> logger) : base(logger)
    {
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        Logger.LogInformation("开始清理过期数据...");

        // 实现数据清理逻辑
        var dataMap = context.MergedJobDataMap;
        var retentionDays = dataMap.GetInt("retentionDays");

        Logger.LogInformation("清理 {RetentionDays} 天前的数据", retentionDays);

        await Task.Delay(500);

        Logger.LogInformation("数据清理完成");
    }
}

/// <summary>
/// 健康检查任务示例
/// </summary>
[ScheduledJob(
    JobName = "HealthCheckJob",
    JobGroup = "Monitoring",
    CronExpression = "0 */10 * * * ?",  // 每10分钟执行一次
    Description = "系统健康检查",
    AutoStart = false,
    Priority = 10)]
public class HealthCheckJob : XiHanScheduledJobBase
{
    public HealthCheckJob(ILogger<HealthCheckJob> logger) : base(logger)
    {
    }

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        Logger.LogInformation("开始系统健康检查...");

        // 实现健康检查逻辑
        await Task.Delay(100);

        Logger.LogInformation("系统健康检查完成，状态正常");
    }

    protected override Task OnExecuteFailedAsync(IJobExecutionContext context, Exception exception)
    {
        Logger.LogCritical(exception, "系统健康检查失败！需要立即处理");
        // 发送告警通知
        return Task.CompletedTask;
    }
}

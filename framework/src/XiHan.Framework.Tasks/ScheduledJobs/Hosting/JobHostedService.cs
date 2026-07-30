// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Hosting;

/// <summary>
/// 任务调度后台服务
/// </summary>
public class JobHostedService : IHostedService
{
    private readonly IJobScheduler _scheduler;
    private readonly ILogger<JobHostedService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public JobHostedService(IJobScheduler scheduler, ILogger<JobHostedService> logger)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 启动服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("任务调度服务正在启动...");

        try
        {
            await _scheduler.StartAsync(cancellationToken);
            _logger.LogInformation("任务调度服务已成功启动");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任务调度服务启动失败");
            throw;
        }
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("任务调度服务正在停止...");

        try
        {
            await _scheduler.StopAsync(cancellationToken);
            _logger.LogInformation("任务调度服务已成功停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任务调度服务停止失败");
            throw;
        }
    }
}

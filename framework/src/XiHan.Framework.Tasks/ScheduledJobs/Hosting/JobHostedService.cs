#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JobHostedService
// Guid:5de9c5c4-5f07-4e39-b892-b26b2c9275aa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/07 15:19:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

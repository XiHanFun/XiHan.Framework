#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScheduledJobHostedService
// Guid:6a7b8c9d-0e1f-2a3b-4c5d-6e7f8a9b0c1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/6 22:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Tasks.ScheduledJobs;

/// <summary>
/// 调度任务托管服务
/// 负责在应用启动时启动调度器，停止时停止调度器
/// </summary>
public class ScheduledJobHostedService : IHostedService
{
    private readonly ILogger<ScheduledJobHostedService> _logger;
    private readonly IScheduledJobManager _jobManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="jobManager">任务管理器</param>
    public ScheduledJobHostedService(
        ILogger<ScheduledJobHostedService> logger,
        IScheduledJobManager jobManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
    }

    /// <summary>
    /// 启动服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("正在启动调度任务服务...");
            await _jobManager.StartAsync();
            _logger.LogInformation("调度任务服务已启动");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动调度任务服务失败");
            throw;
        }
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("正在停止调度任务服务...");
            await _jobManager.StopAsync();
            _logger.LogInformation("调度任务服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止调度任务服务失败");
        }
    }
}

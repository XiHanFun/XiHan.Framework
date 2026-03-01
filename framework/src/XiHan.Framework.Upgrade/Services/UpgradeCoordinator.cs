#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeCoordinator
// Guid:3b86c2a9-0d7b-4d94-9a15-bf2e5f3b6a0d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:28:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Threading;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 升级协调器
/// </summary>
public class UpgradeCoordinator : IUpgradeCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UpgradeCoordinator> _logger;
    private readonly AsyncLock _asyncLock = new();
    private Task? _runningTask;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeFactory">服务范围工厂</param>
    /// <param name="logger">日志记录器</param>
    public UpgradeCoordinator(IServiceScopeFactory scopeFactory, ILogger<UpgradeCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 启动升级任务
    /// </summary>
    /// <returns>升级启动结果</returns>
    public async Task<UpgradeStartResult> StartAsync()
    {
        using (await _asyncLock.LockAsync())
        {
            if (_runningTask != null && !_runningTask.IsCompleted)
            {
                return new UpgradeStartResult
                {
                    Started = false,
                    Status = UpgradeStatus.Upgrading,
                    Message = "升级任务正在执行"
                };
            }

            _runningTask = Task.Run(RunUpgradeAsync);
            return new UpgradeStartResult
            {
                Started = true,
                Status = UpgradeStatus.Upgrading,
                Message = "升级任务已启动"
            };
        }
    }

    /// <summary>
    /// 执行升级任务
    /// </summary>
    /// <returns></returns>
    private async Task RunUpgradeAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<IUpgradeEngine>();
            var result = await engine.ExecuteAsync();
            _logger.LogInformation("升级任务执行完成: {Status} {Message}", result.Status, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "升级任务执行异常: {Error}", ex.Message);
        }
    }
}

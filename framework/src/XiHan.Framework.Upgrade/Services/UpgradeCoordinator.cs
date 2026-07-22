// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

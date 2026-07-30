// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Upgrade.Abstractions;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 默认维护模式管理器（空实现）
/// </summary>
public class DefaultUpgradeMaintenanceModeManager : IUpgradeMaintenanceModeManager
{
    private readonly ILogger<DefaultUpgradeMaintenanceModeManager> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger"></param>
    public DefaultUpgradeMaintenanceModeManager(ILogger<DefaultUpgradeMaintenanceModeManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 进入维护模式（默认空实现）
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task EnterAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("进入维护模式（默认空实现）");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 退出维护模式（默认空实现）
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task ExitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("退出维护模式（默认空实现）");
        return Task.CompletedTask;
    }
}

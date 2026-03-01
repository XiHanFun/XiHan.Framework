#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultUpgradeMaintenanceModeManager
// Guid:9d040d2b-7f01-49d6-9a2d-cb7b7cbec6c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:28:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

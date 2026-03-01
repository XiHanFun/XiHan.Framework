#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullRollingRestartCoordinator
// Guid:5b9f0b8f-4b13-4e7a-9ed9-9f6a2d0c6e1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:28:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Upgrade.Abstractions;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 滚动重启空实现
/// </summary>
public class NullRollingRestartCoordinator : IRollingRestartCoordinator
{
    private readonly ILogger<NullRollingRestartCoordinator> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public NullRollingRestartCoordinator(ILogger<NullRollingRestartCoordinator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 滚动重启（默认空实现）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("滚动重启（默认空实现）");
        return Task.CompletedTask;
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

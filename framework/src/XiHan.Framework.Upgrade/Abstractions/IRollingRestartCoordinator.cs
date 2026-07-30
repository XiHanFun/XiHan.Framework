// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 滚动重启协调器接口
/// </summary>
public interface IRollingRestartCoordinator
{
    /// <summary>
    /// 执行滚动重启
    /// </summary>
    Task RestartAsync(CancellationToken cancellationToken = default);
}

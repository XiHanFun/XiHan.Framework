// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Upgrade.Abstractions;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 程序文件更新空实现
/// </summary>
public class NullUpgradeFileUpdater : IUpgradeFileUpdater
{
    private readonly ILogger<NullUpgradeFileUpdater> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger"></param>
    public NullUpgradeFileUpdater(ILogger<NullUpgradeFileUpdater> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 替换程序文件（默认空实现）
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("替换程序文件（默认空实现）");
        return Task.CompletedTask;
    }
}

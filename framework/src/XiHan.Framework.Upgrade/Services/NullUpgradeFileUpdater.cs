#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullUpgradeFileUpdater
// Guid:0b3ef2a7-6d08-4c0b-8d7d-7e4c4c6b0f52
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:28:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

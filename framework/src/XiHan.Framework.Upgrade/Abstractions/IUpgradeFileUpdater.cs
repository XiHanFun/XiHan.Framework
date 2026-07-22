// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 程序文件更新器接口
/// </summary>
public interface IUpgradeFileUpdater
{
    /// <summary>
    /// 替换程序文件
    /// </summary>
    Task ApplyAsync(CancellationToken cancellationToken = default);
}

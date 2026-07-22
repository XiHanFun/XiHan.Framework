// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Abstractions;

namespace XiHan.Framework.Upgrade.Services;

/// <summary>
/// 默认升级迁移执行器
/// </summary>
public class DefaultUpgradeMigrationExecutor : IUpgradeMigrationExecutor
{
    /// <summary>
    /// 执行迁移脚本
    /// </summary>
    /// <param name="sql">脚本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new InvalidOperationException(
            "未配置 IUpgradeMigrationExecutor 实现，无法执行升级脚本。请在应用层注册该接口后再启用数据库升级。");
    }
}

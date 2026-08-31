// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 模块数据源的 ConfigId 派生规则
/// </summary>
/// <remarks>
/// 模块库的 ConfigId 由「父连接 ConfigId + 模块数据源名」派生，配置里不书写。
/// 这让模块名不占用顶层 ConfigId 命名空间——租户维度住在父 ConfigId 里，
/// 模块维度住在子段里，两者结构上不相交，撞不了名。
/// </remarks>
public static class ModuleDataSourceConfigIds
{
    /// <summary>
    /// 父连接与模块名之间的分隔符
    /// </summary>
    public const string Separator = "_";

    /// <summary>
    /// 派生模块库的 ConfigId
    /// </summary>
    /// <param name="parentConfigId">父连接的 ConfigId</param>
    /// <param name="moduleDataSource">模块数据源名</param>
    /// <returns>形如 <c>Default_Erp</c> / <c>Tenant_1001_Erp</c> 的 ConfigId</returns>
    public static string Build(string parentConfigId, string moduleDataSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentConfigId);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleDataSource);
        return $"{parentConfigId.Trim()}{Separator}{moduleDataSource.Trim()}";
    }
}

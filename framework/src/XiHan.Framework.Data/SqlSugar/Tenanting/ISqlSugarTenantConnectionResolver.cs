// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Tenanting;

/// <summary>
/// SqlSugar 租户连接解析器
/// </summary>
public interface ISqlSugarTenantConnectionResolver
{
    /// <summary>
    /// 解析当前租户连接配置标识
    /// </summary>
    /// <returns></returns>
    string ResolveCurrentConfigId();

    /// <summary>
    /// 根据租户标识解析连接配置标识
    /// </summary>
    /// <param name="tenantId">租户Id</param>
    /// <param name="tenantName">租户名称</param>
    /// <returns></returns>
    string ResolveConfigId(long? tenantId, string? tenantName = null);

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    /// <returns></returns>
    IReadOnlyCollection<string> GetConfigIds();
}

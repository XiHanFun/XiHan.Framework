#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarTenantConnectionResolver
// Guid:f8921968-224d-4e37-8f61-dc2d1bcdf79a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 20:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

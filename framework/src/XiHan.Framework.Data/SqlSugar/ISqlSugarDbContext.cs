#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarDbContext
// Guid:7bc6ad7d-ec18-4f99-aec1-4e7ef54c8c72
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 20:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar 数据上下文
/// </summary>
public interface ISqlSugarDbContext : IDatabaseApi
{
    /// <summary>
    /// 当前租户对应连接的客户端
    /// </summary>
    /// <returns></returns>
    ISqlSugarClient GetClient();

    /// <summary>
    /// 指定配置连接的客户端
    /// </summary>
    /// <param name="connectionConfigId"></param>
    /// <returns></returns>
    ISqlSugarClient GetClient(string? connectionConfigId);

    /// <summary>
    /// 根 SqlSugarScope
    /// </summary>
    SqlSugarScope Scope { get; }

    /// <summary>
    /// 解析当前租户连接配置标识
    /// </summary>
    /// <returns></returns>
    string ResolveCurrentConfigId();
}

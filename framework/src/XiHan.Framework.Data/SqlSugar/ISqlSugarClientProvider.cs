#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarClientProvider
// Guid:4e87b95a-3d1a-4b72-9e5d-6b3b7a6f1a01
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/28 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar 客户端提供器
/// </summary>
public interface ISqlSugarClientProvider : IDatabaseApi
{
    /// <summary>
    /// 获取当前租户对应的客户端
    /// </summary>
    /// <returns></returns>
    ISqlSugarClient GetClient();

    /// <summary>
    /// 获取指定配置标识对应的客户端
    /// </summary>
    /// <param name="connectionConfigId"></param>
    /// <returns></returns>
    ISqlSugarClient GetClient(string? connectionConfigId);

    /// <summary>
    /// 获取 SqlSugarScope
    /// </summary>
    /// <returns></returns>
    SqlSugarScope GetScope();
}

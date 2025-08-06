#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarDbContext
// Guid:8b27c4e3-fc71-4e95-b63a-ff893e99a1d1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 8:30:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// SqlSugar数据库上下文接口
/// </summary>
public interface ISqlSugarDbContext : IDatabaseApi
{
    /// <summary>
    /// 获取SqlSugarClient客户端
    /// </summary>
    /// <returns></returns>
    ISqlSugarClient GetClient();

    /// <summary>
    /// 获取SqlSugarScope客户端
    /// </summary>
    /// <returns></returns>
    SqlSugarScope GetScope();

    /// <summary>
    /// 获取SimpleClient简单客户端
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns></returns>
    SimpleClient<T> GetSimpleClient<T>() where T : class, new();
}

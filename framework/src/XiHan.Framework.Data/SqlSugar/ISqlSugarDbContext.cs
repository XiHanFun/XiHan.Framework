#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarDbContext
// Guid:8b27c4e3-fc71-4e95-b63a-ff893e99a1d1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/11/15 08:30:42
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
    /// 当前租户标识
    /// </summary>
    long? CurrentTenantId { get; }

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
    /// 创建带租户隔离的查询
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns></returns>
    ISugarQueryable<TEntity> CreateQueryable<TEntity>() where TEntity : class, new();

    /// <summary>
    /// 尝试写入实体租户标识
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="entity"></param>
    void TrySetTenantId<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// 临时禁用租户过滤
    /// </summary>
    /// <returns></returns>
    IDisposable DisableTenantFilter();

    /// <summary>
    /// 获取当前作用域服务
    /// </summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <returns>服务实例</returns>
    TService? GetService<TService>() where TService : class;
}

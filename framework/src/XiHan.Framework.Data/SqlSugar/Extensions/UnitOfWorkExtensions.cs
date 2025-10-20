#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkExtensions
// Guid:8c3ef0a5-08c3-4f9a-b3e3-fc4a0a7b8124
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 9:00:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// 工作单元扩展方法
/// </summary>
public static class UnitOfWorkExtensions
{
    /// <summary>
    /// 获取SqlSugar数据库上下文
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns></returns>
    public static ISqlSugarDbContext GetSqlSugarDbContext(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider)
    {
        return (ISqlSugarDbContext)unitOfWork.GetOrAddDatabaseApi(
            "SqlSugarDbContext",
            serviceProvider.GetRequiredService<ISqlSugarDbContext>);
    }

    /// <summary>
    /// 获取SqlSugar客户端
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns></returns>
    public static ISqlSugarClient GetSqlSugarClient(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider)
    {
        return unitOfWork.GetSqlSugarDbContext(serviceProvider).GetClient();
    }

    /// <summary>
    /// 获取SqlSugar查询器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns></returns>
    public static ISugarQueryable<TEntity> GetSqlSugarQueryable<TEntity>(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider) where TEntity : class, new()
    {
        return unitOfWork.GetSqlSugarClient(serviceProvider).Queryable<TEntity>();
    }

    /// <summary>
    /// 获取SimpleClient客户端
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns></returns>
    public static SimpleClient<TEntity> GetSimpleClient<TEntity>(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider) where TEntity : class, new()
    {
        return unitOfWork.GetSqlSugarDbContext(serviceProvider).GetSimpleClient<TEntity>();
    }
}

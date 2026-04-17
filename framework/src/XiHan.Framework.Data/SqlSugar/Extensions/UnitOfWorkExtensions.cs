#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkExtensions
// Guid:8c3ef0a5-08c3-4f9a-b3e3-fc4a0a7b8124
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/11/15 09:00:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// 工作单元扩展方法
/// </summary>
public static class UnitOfWorkExtensions
{
    private const string SqlSugarApiKey = "SqlSugarClientResolver";

    /// <summary>
    /// 获取 SqlSugar 客户端解析器（作为 UoW 的 IDatabaseApi 持有）
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>SqlSugar 客户端解析器</returns>
    public static ISqlSugarClientResolver GetSqlSugarClientResolver(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider)
    {
        return (ISqlSugarClientResolver)unitOfWork.GetOrAddDatabaseApi(
            SqlSugarApiKey,
            serviceProvider.GetRequiredService<ISqlSugarClientResolver>);
    }

    /// <summary>
    /// 获取当前租户对应的 SqlSugar 客户端
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>SqlSugar 客户端</returns>
    public static ISqlSugarClient GetSqlSugarClient(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider)
    {
        return unitOfWork.GetSqlSugarClientResolver(serviceProvider).GetCurrentClient();
    }

    /// <summary>
    /// 获取指定配置的 SqlSugar 客户端
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="configId">连接配置标识</param>
    /// <returns>SqlSugar 客户端</returns>
    public static ISqlSugarClient GetSqlSugarClient(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider, string configId)
    {
        return unitOfWork.GetSqlSugarClientResolver(serviceProvider).GetClient(configId);
    }

    /// <summary>
    /// 获取 SqlSugar 查询器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>SqlSugar 查询器</returns>
    public static ISugarQueryable<TEntity> GetSqlSugarQueryable<TEntity>(this IUnitOfWork unitOfWork, IServiceProvider serviceProvider)
        where TEntity : class, new()
    {
        return unitOfWork.GetSqlSugarClient(serviceProvider).Queryable<TEntity>();
    }
}

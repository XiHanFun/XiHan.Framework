#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarRepository
// Guid:d89f3a1a-843c-4b56-bccd-5a96c7e29db3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 8:50:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.SqlSugarCore.Repository;

/// <summary>
/// SqlSugar仓储接口
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface ISqlSugarRepository<TEntity> : ITransientDependency where TEntity : class, new()
{
    /// <summary>
    /// 获取实体查询器
    /// </summary>
    /// <returns></returns>
    ISugarQueryable<TEntity> GetQueryable();

    /// <summary>
    /// 获取第一个或默认实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    TEntity First(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// 异步获取第一个或默认实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取实体列表
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    List<TEntity> GetList(Expression<Func<TEntity, bool>>? predicate = null);

    /// <summary>
    /// 异步获取实体列表
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    int Insert(TEntity entity);

    /// <summary>
    /// 异步插入实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<int> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    int Update(TEntity entity);

    /// <summary>
    /// 异步更新实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns></returns>
    int Delete(TEntity entity);

    /// <summary>
    /// 异步删除实体
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<int> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件删除实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    int Delete(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// 异步根据条件删除实体
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取计数
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <returns></returns>
    int Count(Expression<Func<TEntity, bool>>? predicate = null);

    /// <summary>
    /// 异步获取计数
    /// </summary>
    /// <param name="predicate">条件表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
}

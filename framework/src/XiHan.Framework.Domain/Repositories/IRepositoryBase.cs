#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRepositoryBase
// Guid:3b5f56c2-6a67-447b-ad9c-d4f004f2c40f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 06:24:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 仓储接口基类，在只读操作的基础上提供增删改能力
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IRepositoryBase<TEntity, TKey> : IReadOnlyRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 添加实体
    /// </summary>
    /// <param name="entity">待添加的实体实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>已持久化的实体实例</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加实体并返回主键
    /// </summary>
    /// <param name="entity">待添加的实体实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>已持久化的实体主键</returns>
    Task<TKey> AddReturnIdAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加实体
    /// </summary>
    /// <param name="entities">待添加的实体集合（只需遍历）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>已持久化的只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">待更新的实体实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>更新后的实体实例</returns>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实体某列数据
    /// </summary>
    /// <param name="columns">待更新的实体实例</param>
    /// <param name="whereExpression">用于筛选待更新实体的条件表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步更新操作的任务</returns>
    Task<bool> UpdateAsync(Expression<Func<TEntity, TEntity>> columns, Expression<Func<TEntity, bool>> whereExpression, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新实体
    /// </summary>
    /// <param name="entities">待更新的实体集合（只需遍历）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>更新后的只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增或更新实体
    /// </summary>
    /// <param name="entity">待新增或更新的实体实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步新增或更新操作的任务</returns>
    Task<bool> AddOrUpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量新增或更新实体
    /// </summary>
    /// <param name="entities">待新增或更新的实体集合（只需遍历）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步新增或更新操作的任务</returns>
    Task<bool> AddOrUpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="entity">待删除的实体实例</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步删除操作的任务</returns>
    Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据主键删除实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步删除操作的任务</returns>
    Task<bool> DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除实体
    /// </summary>
    /// <param name="entities">待删除的实体集合（只需遍历）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步删除操作的任务</returns>
    Task<bool> DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除实体（根据主键）
    /// </summary>
    /// <param name="ids">需要删除的实体主键集合（只需遍历）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步删除操作的任务</returns>
    Task<bool> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件删除实体
    /// </summary>
    /// <param name="predicate">用于筛选待删除实体的条件表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步删除操作的任务</returns>
    Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用事务执行操作
    /// </summary>
    /// <param name="action">需要在事务中执行的操作</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>表示异步事务操作的任务</returns>
    Task<bool> UseTranAsync(Func<Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用事务执行操作并返回结果
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">需要在事务中执行的操作</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>事务操作的结果</returns>
    Task<TResult> UseTranAsync<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default);
}

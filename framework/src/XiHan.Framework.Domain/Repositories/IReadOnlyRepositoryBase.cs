#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IReadOnlyRepositoryBase
// Guid:08ae7e59-f747-4d17-843d-b88f8e19654d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 6:24:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 只读仓储接口基类
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IReadOnlyRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 根据主键查找实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体</returns>
    Task<TEntity?> FindByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件查找单个实体
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体</returns>
    Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约查找单个实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体</returns>
    Task<TEntity?> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取实体集合
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约获取实体集合
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取总数
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体总数</returns>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取总数
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>满足条件的实体总数</returns>
    Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约获取总数
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>满足规约的实体总数</returns>
    Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否存在满足条件的实体
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果存在返回 true，否则返回 false</returns>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否存在满足规约的实体
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果存在返回 true，否则返回 false</returns>
    Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);
}

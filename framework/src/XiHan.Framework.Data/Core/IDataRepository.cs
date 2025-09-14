#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDataRepository
// Guid:a1b2c3d4-e5f6-1234-5678-901234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 21:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Repositories;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.Core;

/// <summary>
/// 数据访问仓储基础接口
/// 为Domain层仓储提供数据访问实现
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IDataRepository<TEntity, TKey> : IRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>, new()
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 获取可查询的实体集合
    /// 注意：此方法返回的IQueryable应谨慎使用，确保在工作单元范围内使用
    /// </summary>
    /// <returns>可查询的实体集合</returns>
    IQueryable<TEntity> GetQueryable();

    /// <summary>
    /// 根据规约查询实体列表
    /// </summary>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    Task<IEnumerable<TEntity>> FindListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据表达式查询
    /// </summary>
    /// <param name="predicate">查询表达式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    Task<IEnumerable<TEntity>> FindListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行原生SQL查询
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体列表</returns>
    Task<IEnumerable<TEntity>> SqlQueryAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);
}

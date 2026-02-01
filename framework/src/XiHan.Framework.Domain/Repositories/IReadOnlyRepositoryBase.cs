#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IReadOnlyRepositoryBase
// Guid:08ae7e59-f747-4d17-843d-b88f8e19654d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 06:24:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 只读仓储接口基类，提供查询相关的通用操作
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IReadOnlyRepositoryBase<TEntity, TKey>
    where TEntity : class, IEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 根据主键获取实体
    /// </summary>
    /// <param name="id">实体主键</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>匹配的实体，如果不存在则返回 <c>null</c></returns>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据主键集合批量获取实体
    /// </summary>
    /// <param name="ids">实体主键集合（只需遍历）</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>与主键集合匹配的只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取单个实体
    /// </summary>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>匹配的实体，如果不存在则返回 <c>null</c></returns>
    Task<TEntity?> GetFirstAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约获取单个实体
    /// </summary>
    /// <param name="specification">定义查询条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>匹配的实体，如果不存在则返回 <c>null</c></returns>
    Task<TEntity?> GetFirstAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取实体集合
    /// </summary>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取实体集合
    /// </summary>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="orderBy">用于排序实体的表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, object>> orderBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约获取实体集合
    /// </summary>
    /// <param name="specification">定义查询条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>只读实体集合（提供 Count 和索引访问）</returns>
    Task<IReadOnlyList<TEntity>> GetListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取总数
    /// </summary>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>当前仓储中的实体数量</returns>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取总数
    /// </summary>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>满足条件的实体数量</returns>
    Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约获取总数
    /// </summary>
    /// <param name="specification">定义查询条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>满足规约的实体数量</returns>
    Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否存在满足条件的实体
    /// </summary>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>如果存在满足条件的实体则返回 <c>true</c>，否则返回 <c>false</c></returns>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否存在满足规约的实体
    /// </summary>
    /// <param name="specification">定义查询条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>如果存在满足规约的实体则返回 <c>true</c>，否则返回 <c>false</c></returns>
    Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    #region 分页查询

    /// <summary>
    /// 获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>分页结果</returns>
    Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>分页结果</returns>
    Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件获取分页数据（支持排序）
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="orderBy">用于排序实体的表达式</param>
    /// <param name="isAscending">是否升序排序</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>分页结果</returns>
    Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, Expression<Func<TEntity, bool>>? predicate, Expression<Func<TEntity, object>> orderBy, bool isAscending = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规约获取分页数据
    /// </summary>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="specification">定义查询条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>分页结果</returns>
    Task<PageResponse<TEntity>> GetPagedAsync(int pageIndex, int pageSize, ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据分页查询对象获取分页数据
    /// </summary>
    /// <param name="query">分页查询对象</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>分页结果</returns>
    Task<PageResponse<TEntity>> GetPagedAsync(PageQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据分页查询对象和条件获取分页数据
    /// </summary>
    /// <param name="query">分页查询对象</param>
    /// <param name="predicate">用于过滤实体的表达式</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>分页结果</returns>
    Task<PageResponse<TEntity>> GetPagedAsync(PageQuery query, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据分页查询对象和规约获取分页数据
    /// </summary>
    /// <param name="query">分页查询对象</param>
    /// <param name="specification">定义查询条件的规约</param>
    /// <param name="cancellationToken">用于取消操作的标记</param>
    /// <returns>分页结果</returns>
    Task<PageResponse<TEntity>> GetPagedAsync(PageQuery query, ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    #endregion 分页查询
}

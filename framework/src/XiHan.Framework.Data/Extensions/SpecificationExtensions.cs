#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SpecificationExtensions
// Guid:e5f6a7b8-c9d0-5678-9012-345678901234
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.Extensions;

/// <summary>
/// 规约扩展方法
/// 为SqlSugar提供规约模式支持
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// 应用规约到SqlSugar查询
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="specification">规约</param>
    /// <returns>应用规约后的查询对象</returns>
    public static ISugarQueryable<T> Where<T>(this ISugarQueryable<T> queryable, ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);

        return queryable.Where(specification.ToExpression());
    }

    /// <summary>
    /// 检查规约是否满足（异步）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在满足规约的实体</returns>
    public static Task<bool> AnyAsync<T>(this ISugarQueryable<T> queryable, ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);

        return queryable.AnyAsync(specification.ToExpression(), cancellationToken);
    }

    /// <summary>
    /// 统计满足规约的实体数量（异步）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>满足规约的实体数量</returns>
    public static Task<int> CountAsync<T>(this ISugarQueryable<T> queryable, ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);

        return queryable.CountAsync(specification.ToExpression(), cancellationToken);
    }

    /// <summary>
    /// 获取第一个满足规约的实体（异步）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>第一个满足规约的实体</returns>
    public static Task<T> FirstAsync<T>(this ISugarQueryable<T> queryable, ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);

        return queryable.FirstAsync(specification.ToExpression(), cancellationToken);
    }

    /// <summary>
    /// 获取第一个满足规约的实体或默认值（异步）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>第一个满足规约的实体或默认值</returns>
    public static Task<T> FirstOrDefaultAsync<T>(this ISugarQueryable<T> queryable, ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);

        return queryable.FirstAsync(specification.ToExpression(), cancellationToken);
    }

    /// <summary>
    /// 获取满足规约的实体列表（异步）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>满足规约的实体列表</returns>
    public static Task<List<T>> ToListAsync<T>(this ISugarQueryable<T> queryable, ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);

        return queryable.Where(specification.ToExpression()).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 分页查询满足规约的实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="queryable">查询对象</param>
    /// <param name="specification">规约</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">页大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    public static async Task<PageResponse<T>> ToPageAsync<T>(this ISugarQueryable<T> queryable, ISpecification<T> specification, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(specification);

        if (pageIndex < 1)
        {
            throw new ArgumentException("页码必须从1开始", nameof(pageIndex));
        }

        if (pageSize < 1)
        {
            throw new ArgumentException("页大小必须大于0", nameof(pageSize));
        }

        var filteredQuery = queryable.Where(specification);

        // 获取总数
        var totalCount = await filteredQuery.CountAsync(cancellationToken);

        // 获取分页数据
        var items = await filteredQuery
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResponse<T>
        {
            Items = items,
            PageData = new PageData
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            }
        };
    }
}

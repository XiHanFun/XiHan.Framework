#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarSpecificationExtensions
// Guid:7b9a62a5-4f2d-4c1d-8f1b-2c5f38d92913
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 06:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Repository.Extensions;

/// <summary>
/// SqlSugar 规约扩展
/// </summary>
public static class SqlSugarSpecificationExtensions
{
    /// <summary>
    /// 根据规约构建查询
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="query">查询对象</param>
    /// <param name="specification">规约</param>
    /// <returns>应用规约后的查询</returns>
    public static ISugarQueryable<TEntity> ApplySpecification<TEntity>(this ISugarQueryable<TEntity> query, ISpecification<TEntity> specification)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(specification);

        return query.Where(specification.ToExpression());
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace XiHan.Framework.Utils.Linq.Expressions;

/// <summary>
/// 可查询扩展方法
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// 如果给定的条件为真，则使用给定的谓词对 <see cref="IQueryable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="condition">第三方条件</param>
    /// <param name="predicate">用于选择查询对象的谓词</param>
    /// <returns>基于 <paramref name="condition"/> 的选择或未选择的查询对象</returns>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }
}

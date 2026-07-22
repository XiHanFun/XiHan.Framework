// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace XiHan.Framework.Domain.Specifications.Abstracts;

/// <summary>
/// 规约接口
/// 用于封装查询逻辑和业务规则
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    /// 转换为表达式
    /// </summary>
    /// <returns>查询表达式</returns>
    Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// 检查实体是否满足规约
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>如果满足返回 true，否则返回 false</returns>
    bool IsSatisfiedBy(T entity);

    /// <summary>
    /// 与运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的规约</returns>
    ISpecification<T> And(ISpecification<T> specification);

    /// <summary>
    /// 或运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的规约</returns>
    ISpecification<T> Or(ISpecification<T> specification);

    /// <summary>
    /// 非运算
    /// </summary>
    /// <returns>取反后的规约</returns>
    ISpecification<T> Not();
}

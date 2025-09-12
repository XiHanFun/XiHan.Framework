#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Specification
// Guid:nop12f9d-8e3a-4b5c-9a2e-1234567890mn
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;

namespace XiHan.Framework.Domain.Specifications;

/// <summary>
/// 规约基类
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
    /// <summary>
    /// 转换为表达式
    /// </summary>
    /// <returns>查询表达式</returns>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// 检查实体是否满足规约
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>如果满足返回 true，否则返回 false</returns>
    public virtual bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }

    /// <summary>
    /// 与运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的规约</returns>
    public ISpecification<T> And(ISpecification<T> specification)
    {
        if (specification == null)
        {
            throw new ArgumentNullException(nameof(specification));
        }

        return new AndSpecification<T>(this, specification);
    }

    /// <summary>
    /// 或运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的规约</returns>
    public ISpecification<T> Or(ISpecification<T> specification)
    {
        if (specification == null)
        {
            throw new ArgumentNullException(nameof(specification));
        }

        return new OrSpecification<T>(this, specification);
    }

    /// <summary>
    /// 非运算
    /// </summary>
    /// <returns>取反后的规约</returns>
    public ISpecification<T> Not()
    {
        return new NotSpecification<T>(this);
    }

    /// <summary>
    /// 隐式转换为表达式
    /// </summary>
    /// <param name="specification">规约</param>
    /// <returns>表达式</returns>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        return specification.ToExpression();
    }

    /// <summary>
    /// 与运算符重载
    /// </summary>
    /// <param name="left">左规约</param>
    /// <param name="right">右规约</param>
    /// <returns>组合后的规约</returns>
    public static ISpecification<T> operator &(Specification<T> left, ISpecification<T> right)
    {
        return left.And(right);
    }

    /// <summary>
    /// 或运算符重载
    /// </summary>
    /// <param name="left">左规约</param>
    /// <param name="right">右规约</param>
    /// <returns>组合后的规约</returns>
    public static ISpecification<T> operator |(Specification<T> left, ISpecification<T> right)
    {
        return left.Or(right);
    }

    /// <summary>
    /// 非运算符重载
    /// </summary>
    /// <param name="specification">规约</param>
    /// <returns>取反后的规约</returns>
    public static ISpecification<T> operator !(Specification<T> specification)
    {
        return specification.Not();
    }
}

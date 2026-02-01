#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Specification
// Guid:012c0f3a-9660-4cc2-84b8-d9b80369b176
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Domain.Specifications;

/// <summary>
/// 规约基类
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
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
        ArgumentNullException.ThrowIfNull(specification);

        return new AndSpecification<T>(this, specification);
    }

    /// <summary>
    /// 或运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的规约</returns>
    public ISpecification<T> Or(ISpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

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
}

/// <summary>
/// 与规约
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
internal class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = new ParameterReplacer(leftExpression.Parameters[0], parameter).Visit(leftExpression.Body);
        var rightBody = new ParameterReplacer(rightExpression.Parameters[0], parameter).Visit(rightExpression.Body);

        var andExpression = Expression.AndAlso(leftBody, rightBody);
        return Expression.Lambda<Func<T, bool>>(andExpression, parameter);
    }
}

/// <summary>
/// 或规约
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
internal class OrSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public OrSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = new ParameterReplacer(leftExpression.Parameters[0], parameter).Visit(leftExpression.Body);
        var rightBody = new ParameterReplacer(rightExpression.Parameters[0], parameter).Visit(rightExpression.Body);

        var orExpression = Expression.OrElse(leftBody, rightBody);
        return Expression.Lambda<Func<T, bool>>(orExpression, parameter);
    }
}

/// <summary>
/// 非规约
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
internal class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _specification;

    public NotSpecification(ISpecification<T> specification)
    {
        _specification = specification ?? throw new ArgumentNullException(nameof(specification));
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _specification.ToExpression();
        var parameter = expression.Parameters[0];
        var notExpression = Expression.Not(expression.Body);

        return Expression.Lambda<Func<T, bool>>(notExpression, parameter);
    }
}

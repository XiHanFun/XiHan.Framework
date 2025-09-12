#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CompositeSpecifications
// Guid:qrs12f9d-8e3a-4b5c-9a2e-1234567890qr
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;

namespace XiHan.Framework.Domain.Specifications;

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

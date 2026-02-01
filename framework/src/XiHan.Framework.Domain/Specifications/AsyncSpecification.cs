#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AsyncSpecification
// Guid:abc12f9d-8e3a-4b5c-9a2e-1234567890ab
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 18:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Specifications.Abstracts;

namespace XiHan.Framework.Domain.Specifications;

/// <summary>
/// 异步规约基类
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public abstract class AsyncSpecification<T> : Specification<T>, IAsyncSpecification<T>
{
    /// <summary>
    /// 异步检查实体是否满足规约
    /// 默认实现：将同步方法包装为异步
    /// 子类可以重写以提供真正的异步实现
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果满足返回 true，否则返回 false</returns>
    public virtual Task<bool> IsSatisfiedByAsync(T entity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IsSatisfiedBy(entity));
    }

    /// <summary>
    /// 异步与运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的异步规约</returns>
    /// <exception cref="ArgumentNullException">当规约为空时抛出</exception>
    public IAsyncSpecification<T> AndAsync(IAsyncSpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return new AsyncAndSpecification<T>(this, specification);
    }

    /// <summary>
    /// 异步或运算
    /// </summary>
    /// <param name="specification">另一个规约</param>
    /// <returns>组合后的异步规约</returns>
    /// <exception cref="ArgumentNullException">当规约为空时抛出</exception>
    public IAsyncSpecification<T> OrAsync(IAsyncSpecification<T> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return new AsyncOrSpecification<T>(this, specification);
    }

    /// <summary>
    /// 异步非运算
    /// </summary>
    /// <returns>取反后的异步规约</returns>
    public IAsyncSpecification<T> NotAsync()
    {
        return new AsyncNotSpecification<T>(this);
    }
}

/// <summary>
/// 异步与规约
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
internal class AsyncAndSpecification<T> : AsyncSpecification<T>
{
    private readonly IAsyncSpecification<T> _left;
    private readonly IAsyncSpecification<T> _right;

    public AsyncAndSpecification(IAsyncSpecification<T> left, IAsyncSpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = new ParameterReplacer(leftExpression.Parameters[0], parameter).Visit(leftExpression.Body);
        var rightBody = new ParameterReplacer(rightExpression.Parameters[0], parameter).Visit(rightExpression.Body);

        var andExpression = Expression.AndAlso(leftBody!, rightBody!);
        return Expression.Lambda<Func<T, bool>>(andExpression, parameter);
    }

    public override async Task<bool> IsSatisfiedByAsync(T entity, CancellationToken cancellationToken = default)
    {
        var leftResult = await _left.IsSatisfiedByAsync(entity, cancellationToken);
        if (!leftResult)
        {
            return false; // 短路求值
        }

        return await _right.IsSatisfiedByAsync(entity, cancellationToken);
    }
}

/// <summary>
/// 异步或规约
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
internal class AsyncOrSpecification<T> : AsyncSpecification<T>
{
    private readonly IAsyncSpecification<T> _left;
    private readonly IAsyncSpecification<T> _right;

    public AsyncOrSpecification(IAsyncSpecification<T> left, IAsyncSpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = new ParameterReplacer(leftExpression.Parameters[0], parameter).Visit(leftExpression.Body);
        var rightBody = new ParameterReplacer(rightExpression.Parameters[0], parameter).Visit(rightExpression.Body);

        var orExpression = Expression.OrElse(leftBody!, rightBody!);
        return Expression.Lambda<Func<T, bool>>(orExpression, parameter);
    }

    public override async Task<bool> IsSatisfiedByAsync(T entity, CancellationToken cancellationToken = default)
    {
        var leftResult = await _left.IsSatisfiedByAsync(entity, cancellationToken);
        if (leftResult)
        {
            return true; // 短路求值
        }

        return await _right.IsSatisfiedByAsync(entity, cancellationToken);
    }
}

/// <summary>
/// 异步非规约
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
internal class AsyncNotSpecification<T> : AsyncSpecification<T>
{
    private readonly IAsyncSpecification<T> _specification;

    public AsyncNotSpecification(IAsyncSpecification<T> specification)
    {
        _specification = specification;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _specification.ToExpression();
        var parameter = expression.Parameters[0];
        var notExpression = Expression.Not(expression.Body);

        return Expression.Lambda<Func<T, bool>>(notExpression, parameter);
    }

    public override async Task<bool> IsSatisfiedByAsync(T entity, CancellationToken cancellationToken = default)
    {
        var result = await _specification.IsSatisfiedByAsync(entity, cancellationToken);
        return !result;
    }
}

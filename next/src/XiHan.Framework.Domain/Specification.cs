// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// 查询规约：同一条件既能在内存判定，也能交给查询提供器翻译。
/// </summary>
/// <typeparam name="T">候选对象类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface ISpecification<T>
{
    /// <summary>
    /// 条件表达式。
    /// </summary>
    Expression<Func<T, bool>> Criteria { get; }

    /// <summary>
    /// 在内存中判定。
    /// </summary>
    bool IsSatisfiedBy(T candidate);
}

/// <summary>
/// <see cref="Specification{T}"/> 的工厂。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class Specification
{
    /// <summary>
    /// 恒真规约。
    /// </summary>
    public static Specification<T> All<T>() => AllOf<T>.Instance;

    /// <summary>
    /// 用表达式创建规约。
    /// </summary>
    public static Specification<T> Of<T>(Expression<Func<T, bool>> criteria) => new(criteria);

    private static class AllOf<T>
    {
        public static readonly Specification<T> Instance = new(static _ => true);
    }
}

/// <summary>
/// 规约基类：持有表达式，惰性编译内存判定，提供 And / Or / Not 组合。
/// </summary>
/// <typeparam name="T">候选对象类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public class Specification<T> : ISpecification<T>
{
    private Func<T, bool>? _predicate;

    /// <summary>
    /// 用表达式创建规约。
    /// </summary>
    public Specification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria ?? throw new ArgumentNullException(nameof(criteria));
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>> Criteria { get; }

    /// <inheritdoc />
    public bool IsSatisfiedBy(T candidate) => (_predicate ??= Criteria.Compile())(candidate);

    /// <summary>
    /// 与。
    /// </summary>
    public Specification<T> And(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Specification<T>(Combine(Criteria, other.Criteria, Expression.AndAlso));
    }

    /// <summary>
    /// 或。
    /// </summary>
    public Specification<T> Or(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Specification<T>(Combine(Criteria, other.Criteria, Expression.OrElse));
    }

    /// <summary>
    /// 非。
    /// </summary>
    public Specification<T> Not()
        => new(Expression.Lambda<Func<T, bool>>(Expression.Not(Criteria.Body), Criteria.Parameters));

    private static Expression<Func<T, bool>> Combine(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(merge(left.Body, rightBody), parameter);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}

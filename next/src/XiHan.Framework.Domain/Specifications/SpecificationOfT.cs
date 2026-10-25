// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain.Specifications;

/// <summary>
/// 规约基类：持有表达式，惰性编译内存判定，提供 And / Or / Not 组合。
/// </summary>
/// <typeparam name="T">候选对象类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public class Specification<T> : ISpecification<T>
{
    /// <summary>
    /// <see cref="Criteria"/> 编译后的委托；首次内存判定时才编译，只交给查询提供器翻译的规约不付编译代价。
    /// </summary>
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

    /// <summary>
    /// 用 <paramref name="merge"/> 把两个表达式的主体合并成一个新的 lambda。
    /// </summary>
    private static Expression<Func<T, bool>> Combine(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        // 两个 lambda 各有自己的参数；必须把右侧主体里的参数替换成左侧参数，合并后的表达式才只有一个参数，
        // 否则 LINQ 提供器（如 EF Core）无法翻译
        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(merge(left.Body, rightBody), parameter);
    }

    /// <summary>
    /// 把表达式树里的参数 <paramref name="from"/> 全部替换为 <paramref name="to"/>。
    /// </summary>
    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        /// <summary>
        /// 命中待替换参数时返回目标参数，其余节点交给基类处理。
        /// </summary>
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}

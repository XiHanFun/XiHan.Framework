// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Reflection;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 把规约里可空值类型与 null 的相等 / 不等改写成 <c>HasValue</c> 判定。
/// </summary>
/// <remarks>
/// 再交给 SqlSugar 翻译。C# 把 <c>x.RevokedAt == null</c>（<c>DateTimeOffset?</c>）编译成带 <c>op_Equality</c> 方法的提升比较，SqlSugar 据此生成 <c>WHERE revoked_at = NULL</c>——永假、静默返回空集；<c>string?</c> 的 <c>== null</c> 没有运算符方法，正确译成 <c>IS NULL</c>。改写后两者一致：<c>== null</c> → <c>!x.RevokedAt.HasValue</c>，<c>!= null</c> → <c>x.RevokedAt.HasValue</c>。null 一侧可以是字面量，也可以是闭包捕获的变量或静态成员（求值为 null 时同样改写；值非 null 时不动，由 SqlSugar 作为参数翻译）。
/// </remarks>
internal sealed class NullableComparisonRewriter : ExpressionVisitor
{
    /// <summary>
    /// 共享实例：访问器无状态。
    /// </summary>
    private static readonly NullableComparisonRewriter _instance = new();

    /// <summary>
    /// 改写规约。
    /// </summary>
    /// <remarks>
    /// 没有可空值类型判空的表达式原样返回（同一实例）。
    /// </remarks>
    public static Expression<Func<TEntity, bool>> Rewrite<TEntity>(Expression<Func<TEntity, bool>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return (Expression<Func<TEntity, bool>>)_instance.Visit(criteria);
    }

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            if (TryRewrite(node.Left, node.Right, node.NodeType == ExpressionType.Equal, out var rewritten)
                || TryRewrite(node.Right, node.Left, node.NodeType == ExpressionType.Equal, out rewritten))
            {
                return rewritten;
            }
        }

        return base.VisitBinary(node);
    }

    /// <summary>
    /// <paramref name="candidate"/> 是可空值类型、<paramref name="other"/> 求值为 null 时，改写成 <c>HasValue</c> 判定。
    /// </summary>
    private bool TryRewrite(Expression candidate, Expression other, bool equal, out Expression rewritten)
    {
        rewritten = null!;
        var operand = StripConvert(candidate);
        if (Nullable.GetUnderlyingType(operand.Type) is null || !IsNull(other))
        {
            return false;
        }

        var hasValue = Expression.Property(Visit(operand), nameof(Nullable<int>.HasValue));
        rewritten = equal ? Expression.Not(hasValue) : hasValue;
        return true;
    }

    /// <summary>
    /// 去掉编译器为提升比较加的 <c>Convert</c> 包装。
    /// </summary>
    /// <remarks>
    /// <c>(DateTimeOffset?)x.CreatedAt</c> 之类的显式转换也一并去掉，它们不改变空值语义。
    /// </remarks>
    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary && Nullable.GetUnderlyingType(unary.Type) is not null)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    /// <summary>
    /// 表达式是否为 null。
    /// </summary>
    /// <remarks>
    /// 字面量，或不依赖参数、能静态求值（常量、闭包字段、静态成员、转换）且求出 null 的表达式。
    /// </remarks>
    private static bool IsNull(Expression expression) => TryEvaluate(expression, out var value) && value is null;

    /// <summary>
    /// 不编译地求值。
    /// </summary>
    /// <remarks>
    /// 常量直接取，成员访问沿链取值，转换透传；其他形态（方法调用、参数）不求值。
    /// </remarks>
    private static bool TryEvaluate(Expression expression, out object? value)
    {
        switch (expression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;
            case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary:
                return TryEvaluate(unary.Operand, out value);
            case MemberExpression member:
                object? target = null;
                if (member.Expression is not null && !TryEvaluate(member.Expression, out target))
                {
                    value = null;
                    return false;
                }

                if (member.Expression is not null && target is null)
                {
                    // 实例成员的宿主是 null：不求值，交给 SqlSugar 自己处理
                    value = null;
                    return false;
                }

                switch (member.Member)
                {
                    case FieldInfo field:
                        value = field.GetValue(target);
                        return true;
                    case PropertyInfo { CanRead: true, GetMethod.IsStatic: var isStatic } property when isStatic || target is not null:
                        value = property.GetValue(target);
                        return true;
                    default:
                        value = null;
                        return false;
                }

            default:
                value = null;
                return false;
        }
    }
}

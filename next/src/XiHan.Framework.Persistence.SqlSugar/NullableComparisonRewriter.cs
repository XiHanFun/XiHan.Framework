// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Reflection;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 把规约改写成 SqlSugar 能正确翻译的等价形式。
/// </summary>
/// <remarks>
/// 再交给 SqlSugar 翻译，处理它的两个翻译缺陷：
/// <list type="number">
/// <item>C# 把 <c>x.RevokedAt == null</c>（<c>DateTimeOffset?</c>）编译成带 <c>op_Equality</c> 方法的提升比较，SqlSugar 据此生成 <c>WHERE revoked_at = NULL</c>——永假、静默返回空集（ADR-0035）；<c>string?</c> 的 <c>== null</c> 正确译成 <c>IS NULL</c>。</item>
/// <item>逻辑非（<c>!x.HasValue</c>、<c>!(a == b)</c>、<c>!(p &amp;&amp; q)</c>）作为 <c>&amp;&amp;</c> / <c>||</c> 的左操作数、又处在另一个与 / 或条件的右侧时，SqlSugar 漏写这个运算符：<c>A &amp;&amp; (!x.HasValue || B)</c> 译成 <c>(A) AND (NOT(x IS NOT NULL )(B))</c>，数据库报语法错误。根因在 SqlSugar 的 <c>BinaryExpressionResolve.Left</c>：左操作数是一元表达式时，只在整段结果里没有任何运算符占位符时才补写运算符，而外层与条件的占位符此时还没替换，于是被当成「已写过」（ADR-0035 修订）。布尔字段的取反与方法调用的取反自己写占位符，不受影响。</item>
/// </list>
/// 改写规则：可空值类型与 null 的 <c>==</c> 改写成 <c>SqlFunc.EqualsNull(x, null)</c>（译成 <c>(x is null)</c>，是方法调用而不是一元表达式，处在任何位置都译对），<c>!=</c> 改写成 <c>x.HasValue</c>（译成 <c>(x IS NOT NULL )</c>）；逻辑非一律下推到叶子：<c>!!p</c> → <c>p</c>，<c>!(p &amp;&amp; q)</c> → <c>!p || !q</c>，<c>!(p || q)</c> → <c>!p &amp;&amp; !q</c>，<c>!(a == b)</c> → <c>a != b</c>，<c>!(a &lt; b)</c> → <c>a &gt;= b</c>（其余比较同理），<c>!x.HasValue</c> → <c>SqlFunc.EqualsNull(x, null)</c>；留下的只有布尔字段与方法调用等叶子上的取反。这些等价在 SQL 的三值逻辑下成立（含 NULL 时两边同为未知），改写只用于翻译，内存判定仍用原规约。null 一侧可以是字面量，也可以是闭包捕获的变量或静态成员（求值为 null 时同样改写；值非 null 时不动，由 SqlSugar 作为参数翻译）。
/// </remarks>
internal sealed class NullableComparisonRewriter : ExpressionVisitor
{
    /// <summary>
    /// 共享实例：访问器无状态。
    /// </summary>
    private static readonly NullableComparisonRewriter _instance = new();

    /// <summary>
    /// <c>SqlFunc.EqualsNull</c> 的方法信息。
    /// </summary>
    /// <remarks>
    /// 从表达式里取，不经反射查找。
    /// </remarks>
    private static readonly MethodInfo _equalsNull = ((MethodCallExpression)((Expression<Func<object, bool>>)(static value => SqlFunc.EqualsNull(value, null!))).Body).Method;

    /// <summary>
    /// 改写规约。
    /// </summary>
    /// <remarks>
    /// 没有需要改写的节点时原样返回（同一实例）。
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

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node)
    {
        // 只处理布尔的逻辑非；按位取反（整数）与可空布尔的提升取反原样交给 SqlSugar
        return node.NodeType == ExpressionType.Not && node.Type == typeof(bool) && node.Method is null
            ? Negate(node.Operand)
            : base.VisitUnary(node);
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
    /// 比较运算的反运算。
    /// </summary>
    /// <remarks>
    /// 相等与不等互反，小于与大于等于互反，大于与小于等于互反；其他运算没有。
    /// </remarks>
    private static bool TryInvert(ExpressionType comparison, out ExpressionType inverted)
    {
        inverted = comparison switch
        {
            ExpressionType.Equal => ExpressionType.NotEqual,
            ExpressionType.NotEqual => ExpressionType.Equal,
            ExpressionType.LessThan => ExpressionType.GreaterThanOrEqual,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThan,
            ExpressionType.GreaterThan => ExpressionType.LessThanOrEqual,
            ExpressionType.LessThanOrEqual => ExpressionType.GreaterThan,
            _ => comparison,
        };
        return inverted != comparison;
    }

    /// <summary>
    /// 按操作数类型构造比较。
    /// </summary>
    /// <remarks>
    /// 运算符方法重新解析（DateTimeOffset 的 op_LessThan 换成 op_GreaterThanOrEqual，可空时提升）。自定义类型只定义了一半比较运算符时构造不出来，返回 <see langword="false"/>，调用方保留取反。
    /// </remarks>
    private static bool TryMakeBinary(ExpressionType type, Expression left, Expression right, out Expression binary)
    {
        try
        {
            binary = Expression.MakeBinary(type, left, right);
            return true;
        }
        catch (InvalidOperationException)
        {
            binary = null!;
            return false;
        }
    }

    /// <summary>
    /// 可空值类型的 <c>HasValue</c> 访问。
    /// </summary>
    /// <remarks>
    /// 是的话给出被判空的值表达式。
    /// </remarks>
    private static bool IsHasValue(Expression expression, out Expression value)
    {
        if (expression is MemberExpression { Member.Name: nameof(Nullable<int>.HasValue), Expression: { } target } && Nullable.GetUnderlyingType(target.Type) is not null)
        {
            value = target;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>
    /// 判空：<c>SqlFunc.EqualsNull(value, null)</c>。
    /// </summary>
    private static MethodCallExpression IsNullCall(Expression value)
        => Expression.Call(_equalsNull, Expression.Convert(value, typeof(object)), Expression.Constant(null, typeof(object)));

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

    /// <summary>
    /// <paramref name="candidate"/> 是可空值类型、<paramref name="other"/> 求值为 null 时，改写成判空或 <c>HasValue</c>。
    /// </summary>
    private bool TryRewrite(Expression candidate, Expression other, bool equal, out Expression rewritten)
    {
        rewritten = null!;
        var operand = StripConvert(candidate);
        if (Nullable.GetUnderlyingType(operand.Type) is null || !IsNull(other))
        {
            return false;
        }

        var visited = Visit(operand);
        rewritten = equal ? IsNullCall(visited) : Expression.Property(visited, nameof(Nullable<int>.HasValue));
        return true;
    }

    /// <summary>
    /// 取反并把否定下推到叶子。
    /// </summary>
    /// <remarks>
    /// 双重否定消去，与 / 或按德摩根律展开，比较换成反运算，<c>HasValue</c> 换成判空；别的叶子（布尔字段、方法调用）保留取反。
    /// </remarks>
    private Expression Negate(Expression operand)
    {
        switch (operand)
        {
            case UnaryExpression { NodeType: ExpressionType.Not, Method: null } inner when inner.Type == typeof(bool):
                return Visit(inner.Operand);
            case BinaryExpression { NodeType: ExpressionType.AndAlso } both:
                return Expression.OrElse(Negate(both.Left), Negate(both.Right));
            case BinaryExpression { NodeType: ExpressionType.OrElse } either:
                return Expression.AndAlso(Negate(either.Left), Negate(either.Right));
            case BinaryExpression comparison when TryInvert(comparison.NodeType, out var inverted) && TryMakeBinary(inverted, comparison.Left, comparison.Right, out var opposite):
                // 反运算再走一遍判空改写：!(x == null) 成为 x != null，再成为 x.HasValue
                return Visit(opposite);
            default:
                return IsHasValue(operand, out var value) ? IsNullCall(Visit(value)) : Expression.Not(Visit(operand));
        }
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 查询编译前把 SQLite 上的字符串匹配换成标记方法。
/// </summary>
/// <remarks>
/// 改写 <see cref="string.Contains(string)"/>、<see cref="string.StartsWith(string)"/>、<see cref="string.EndsWith(string)"/> 与它们的字符重载（字符实参先转成字符串），由 <see cref="SqliteStringMatchingTranslator"/> 译成 <c>LIKE</c>。
/// 只在 Provider 是 SQLite 时改写；带 <see cref="StringComparison"/> 等多个实参的重载不在契约内，原样留给 EF Core。
/// 编译前参数已提取，闭包变量是参数节点；编译结果按查询形状缓存，改写只在首次编译时发生。全局查询过滤器在这之后才展开，不经过改写。
/// </remarks>
internal sealed class SqliteStringMatchingInterceptor : IQueryExpressionInterceptor
{
    /// <summary>
    /// <see cref="char.ToString()"/>：字符实参转成字符串。
    /// </summary>
    private static readonly MethodInfo _charToString = typeof(char).GetMethod(nameof(char.ToString), Type.EmptyTypes)!;

    /// <summary>
    /// 创建。
    /// </summary>
    private SqliteStringMatchingInterceptor()
    {
    }

    /// <summary>
    /// 共享实例：拦截器无状态。
    /// </summary>
    public static SqliteStringMatchingInterceptor Instance { get; } = new();

    /// <inheritdoc />
    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        => Rewrite(queryExpression, eventData.Context?.Database.ProviderName);

    /// <summary>
    /// 按 Provider 名改写查询表达式。
    /// </summary>
    /// <remarks>
    /// 不是 SQLite 时原样返回同一个实例。拦截器与测试共用。
    /// </remarks>
    internal static Expression Rewrite(Expression query, string? providerName)
        => string.Equals(providerName, ModelBuilderExtensions.SqliteProvider, StringComparison.Ordinal) ? new Rewriter().Visit(query) : query;

    /// <summary>
    /// 把三个字符串方法换成标记方法的表达式访问器。
    /// </summary>
    private sealed class Rewriter : ExpressionVisitor
    {
        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object is null || node.Method.DeclaringType != typeof(string) || node.Arguments.Count != 1 || Marker(node.Method.Name) is not { } marker)
            {
                return base.VisitMethodCall(node);
            }

            var part = Visit(node.Arguments[0]);
            if (part.Type == typeof(char))
            {
                // 字符常量直接换成字符串常量（翻译时按常量转义）；其余字符实参在 SQL 里转成文本
                part = part is ConstantExpression { Value: char character } ? Expression.Constant(new string(character, 1), typeof(string)) : Expression.Call(part, _charToString);
            }
            else if (part.Type != typeof(string))
            {
                return base.VisitMethodCall(node);
            }

            return Expression.Call(marker, Visit(node.Object), part);
        }

        /// <summary>
        /// 方法名对应的标记方法。
        /// </summary>
        /// <remarks>
        /// 不是这三个方法时为 <see langword="null"/>。
        /// </remarks>
        private static MethodInfo? Marker(string name) => name switch
        {
            nameof(string.Contains) => SqliteStringMatching.ContainsMethod,
            nameof(string.StartsWith) => SqliteStringMatching.StartsWithMethod,
            nameof(string.EndsWith) => SqliteStringMatching.EndsWithMethod,
            _ => null,
        };
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 把字符串匹配的标记方法译成 SQLite 的 <c>LIKE</c>
/// </summary>
/// <remarks>
/// 方法翻译插件，由 <see cref="SqliteStringMatchingExtension"/> 登记到 EF Core 的内部服务容器，只认 <see cref="SqliteStringMatching"/> 的标记方法（只有 SQLite 上的拦截器会写出它们）
/// 译成 <c>被匹配 LIKE 模式 ESCAPE '\'</c>：实参是字符串常量时在翻译时转义、拼上 <c>%</c>；是参数、列或表达式时在 SQL 里以嵌套的 <c>replace</c> 转义、以 <c>||</c> 拼上 <c>%</c>，并要求实参不为 NULL
/// SQLite 的 <c>LIKE</c> 只对 ASCII 字母不区分大小写，三个方法与各种实参因此同一套规则，与 SqlSugar 在 SQLite 上一样（ADR-0101）
/// </remarks>
internal sealed class SqliteStringMatchingTranslator : IMethodCallTranslatorPlugin, IMethodCallTranslator
{
    /// <summary>
    /// <c>LIKE</c> 的转义符：反斜杠
    /// </summary>
    /// <remarks>
    /// SQLite 的字符串字面量里反斜杠没有特殊含义，<c>'\'</c> 就是一个字符；与 EF Core 自己在 SQLite 上的 <c>LIKE</c> 同一个转义符
    /// </remarks>
    private const string EscapeCharacter = "\\";

    /// <summary>
    /// <c>LIKE</c> 的任意串通配符
    /// </summary>
    private const string AnyText = "%";

    /// <summary>
    /// 要转义的字符与替换结果，按替换顺序
    /// </summary>
    /// <remarks>
    /// 转义符自己必须最先替换，否则后面加进去的转义符会被再转义一次
    /// </remarks>
    private static readonly (string Text, string Escaped)[] _escapes = [(EscapeCharacter, EscapeCharacter + EscapeCharacter), ("%", EscapeCharacter + "%"), ("_", EscapeCharacter + "_")];

    /// <summary>
    /// 构建 SQL 表达式的工厂
    /// </summary>
    private readonly ISqlExpressionFactory _sql;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="sqlExpressionFactory">EF Core 内部服务容器里的 SQL 表达式工厂</param>
    public SqliteStringMatchingTranslator(ISqlExpressionFactory sqlExpressionFactory)
    {
        _sql = sqlExpressionFactory;
    }

    /// <inheritdoc />
    public IEnumerable<IMethodCallTranslator> Translators => [this];

    /// <inheritdoc />
    public SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // 前后是否各拼一个 %：包含两边都拼，前缀只拼后面，后缀只拼前面
        (bool Leading, bool Trailing) wildcards;
        if (method.Equals(SqliteStringMatching.ContainsMethod))
        {
            wildcards = (true, true);
        }
        else if (method.Equals(SqliteStringMatching.StartsWithMethod))
        {
            wildcards = (false, true);
        }
        else if (method.Equals(SqliteStringMatching.EndsWithMethod))
        {
            wildcards = (true, false);
        }
        else
        {
            return null;
        }

        // 两边用同一个字符串类型映射：先取已有映射的一边（通常是列），都没有时取字符串的缺省映射
        var mapping = arguments[0].TypeMapping ?? arguments[1].TypeMapping ?? _sql.ApplyDefaultTypeMapping(arguments[0]).TypeMapping;
        var value = _sql.ApplyTypeMapping(arguments[0], mapping);
        var part = _sql.ApplyTypeMapping(arguments[1], mapping);
        var escape = _sql.Constant(EscapeCharacter, mapping);

        if (part is SqlConstantExpression { Value: string text })
        {
            // 常量：模式在翻译时算好，SQL 里是一个转义过的字面量
            var literal = (wildcards.Leading ? AnyText : string.Empty) + Escape(text) + (wildcards.Trailing ? AnyText : string.Empty);
            return _sql.Like(value, _sql.Constant(literal, mapping), escape);
        }

        // 参数、列或表达式：在 SQL 里转义，再拼上通配符；SQL 文本里只有参数名或列，没有实参的值
        var pattern = part;
        foreach (var (from, to) in _escapes)
        {
            pattern = _sql.Function("replace", [pattern, _sql.Constant(from, mapping), _sql.Constant(to, mapping)], nullable: true, argumentsPropagateNullability: [true, true, true], typeof(string), mapping);
        }

        if (wildcards.Leading)
        {
            pattern = _sql.Add(_sql.Constant(AnyText, mapping), pattern, mapping);
        }

        if (wildcards.Trailing)
        {
            pattern = _sql.Add(pattern, _sql.Constant(AnyText, mapping), mapping);
        }

        // EF Core 按 C# 语义把字符串拼接里的 NULL 当空串（加 COALESCE），NULL 实参会变成匹配全部的 %%：
        // 显式要求实参不为 NULL，NULL 实参不匹配，与原来的 instr / LIKE 翻译一样；实参不可空时 EF Core 会把这一条去掉
        return _sql.AndAlso(_sql.Like(value, pattern, escape), _sql.IsNotNull(part));
    }

    /// <summary>
    /// 转义常量里的 <c>LIKE</c> 特殊字符
    /// </summary>
    private static string Escape(string text)
    {
        foreach (var (from, to) in _escapes)
        {
            text = text.Replace(from, to, StringComparison.Ordinal);
        }

        return text;
    }
}

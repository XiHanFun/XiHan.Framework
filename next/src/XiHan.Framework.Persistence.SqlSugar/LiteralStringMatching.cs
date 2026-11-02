// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 把 <c>string.Contains</c> / <c>StartsWith</c> / <c>EndsWith</c> 译成按字面匹配的 <c>LIKE … ESCAPE '!'</c>。
/// </summary>
/// <remarks>
/// SqlSugar 把这三个方法译成不转义的 <c>LIKE</c>：实参里的 <c>_</c>、<c>%</c> 成了通配符，SQL Server 的 <c>[</c> 开字符类，PostgreSQL 与 MySQL 的 <c>LIKE</c> 缺省以反斜杠转义。本类以 SqlSugar 的外部函数（<see cref="ConfigureExternalServices.SqlFuncServices"/>）按这三个名字接管翻译——SqlSugar 解析方法调用时先按名字查外部函数、查到就不走内置翻译，所以经仓储的规约与直接用客户端的查询、删除条件、投影都经过这里（集合的 <c>Contains</c> 先被它改名成 <c>ContainsArray</c>，不受影响）。
/// <list type="bullet">
/// <item>实参是 SqlSugar 为常量或闭包值建的参数：参数值先转义、再拼上通配符，SQL 里只有参数名，不拼接实参。</item>
/// <item>实参是列或 SQL 表达式：在 SQL 里用 <c>REPLACE</c> 逐个转义，拼接的写法与 SqlSugar 原来的翻译相同（PostgreSQL 与 MySQL 用 <c>concat</c>，SQL Server 用 <c>+</c>，SQLite 与 Oracle 用 <c>||</c>），实参为 NULL 时的结果因此不变。</item>
/// <item>转义 <c>%</c>、<c>_</c> 与转义符 <c>!</c> 本身；SQL Server 另转义 <c>[</c>。Oracle 的转义符后面只能跟 <c>%</c>、<c>_</c> 或它自己（否则 ORA-01424），所以别的库不转义 <c>[</c>。</item>
/// <item>连接配置开了 <c>MoreSettings.EnableILike</c> 时照 SqlSugar 原样用 <c>ilike</c>。</item>
/// <item>只在真实库上验证过的 SQLite、PostgreSQL、MySQL、SQL Server、Oracle 上改写；其余数据库沿用 SqlSugar 原来的翻译（不转义）。</item>
/// </list>
/// 大小写与重音是否敏感照各库的 <c>LIKE</c> 与排序规则，不在这里统一。
/// </remarks>
internal static class LiteralStringMatching
{
    /// <summary>
    /// <c>LIKE</c> 的转义符。
    /// </summary>
    /// <remarks>
    /// 不用反斜杠：MySQL 缺省模式下字面量 <c>'\'</c> 没有结束（反斜杠转义了引号），写成 <c>'\\'</c> 在 <c>NO_BACKSLASH_ESCAPES</c> 模式下又是两个字符；PostgreSQL 的 <c>standard_conforming_strings = off</c> 同理。<c>!</c> 在五种数据库的字面量与 <c>LIKE</c> 里都没有特殊含义，SQL 里也就不出现反斜杠，结果不受这些模式影响。
    /// </remarks>
    internal const char EscapeCharacter = '!';

    /// <summary>
    /// 接管的三个方法：名字、模式前后是否加通配符、SqlSugar 原来的翻译。
    /// </summary>
    private static readonly (string Name, bool Leading, bool Trailing, Func<IDbMethods, MethodCallExpressionModel, string> Original)[] _methods =
    [
        ("Contains", true, true, static (methods, model) => methods.Contains(model)),
        ("StartsWith", false, true, static (methods, model) => methods.StartsWith(model)),
        ("EndsWith", true, false, static (methods, model) => methods.EndsWith(model)),
    ];

    /// <summary>
    /// 在外部服务上登记三个方法的翻译。
    /// </summary>
    /// <remarks>
    /// 已有同名的外部函数（应用自己登记的，或此前已施加过约定）不再登记：SqlSugar 按名字取第一个，应用自己的优先；重复施加不重复登记。
    /// </remarks>
    public static void Register(ConfigureExternalServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var functions = services.SqlFuncServices ??= [];
        foreach (var (name, leading, trailing, original) in _methods)
        {
            if (functions.Exists(function => string.Equals(function.UniqueMethodName, name, StringComparison.Ordinal)))
            {
                continue;
            }

            functions.Add(new SqlFuncExternal
            {
                UniqueMethodName = name,
                MethodValue = (model, dbType, context) => Translate(model, dbType, context, name, leading, trailing, original),
            });
        }
    }

    /// <summary>
    /// 转义值里的 <c>LIKE</c> 特殊字符。
    /// </summary>
    /// <remarks>
    /// <c>%</c>、<c>_</c> 与 <see cref="EscapeCharacter"/> 前加转义符；SQL Server 另转义 <c>[</c>。
    /// </remarks>
    private static string Escape(string value, DbType dbType)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            if (character is '%' or '_' or EscapeCharacter || (character == '[' && dbType == DbType.SqlServer))
            {
                builder.Append(EscapeCharacter);
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 是否在这个数据库上改写。
    /// </summary>
    /// <remarks>
    /// 只认在真实库上验证过的五种；MySQL、PostgreSQL、Oracle 的兼容库（TiDB、人大金仓、达梦等）是别的 <see cref="DbType"/>，不在其列。
    /// </remarks>
    private static bool IsSupported(DbType dbType) => dbType is DbType.Sqlite or DbType.PostgreSQL or DbType.MySql or DbType.SqlServer or DbType.Oracle;

    /// <summary>
    /// 一次方法调用的翻译。
    /// </summary>
    /// <remarks>
    /// <paramref name="model"/> 的第一个实参是被匹配的一侧，第二个是要找的子串；带 <c>StringComparison</c> 的重载的第三个实参照 SqlSugar 原样忽略。
    /// </remarks>
    private static string Translate(MethodCallExpressionModel model, DbType reported, ExpressionContext context, string method, bool leading, bool trailing, Func<IDbMethods, MethodCallExpressionModel, string> original)
    {
        // 外部函数收到的数据库类型由表达式上下文的类型推出，兼容库会被归到它模仿的库；连接配置里的才是真实的类型
        var config = context.SugarContext?.Context?.CurrentConnectionConfig;
        var dbType = config?.DbType ?? reported;
        var like = config?.MoreSettings?.EnableILike == true ? "ilike" : "like";
        if (!IsSupported(dbType))
        {
            // 外部函数接管之后，SqlSugar 不再替换 ilike，这里照它的做法替换
            model.Conext ??= context;
            var sql = original(context.DbMehtods, model);
            return like == "ilike" ? sql.Replace(" like ", " ilike ", StringComparison.Ordinal) : sql;
        }

        if (model.Args is not { Count: >= 2 })
        {
            throw new NotSupportedException($"The call to '{method}' cannot be translated: it has {model.Args?.Count ?? 0} argument(s).");
        }

        var subject = model.Args[0].MemberName;
        var pattern = TryBindPattern(model, model.Args[1], dbType, leading, trailing, out var parameterName)
            ? parameterName
            : Concatenate(dbType, leading, EscapeInSql(Operand(model.Args[1], method), dbType), trailing);
        return $" ({subject} {like} {pattern} ESCAPE '{EscapeCharacter}') ";
    }

    /// <summary>
    /// 值实参的参数值换成转义后加通配符的模式。
    /// </summary>
    /// <remarks>
    /// 只处理 SqlSugar 为常量、闭包值或字符建的字符串（或字符）参数。参数是这次方法调用独占的（SqlSugar 为每个实参新建一个 <c>MethodConst</c> 参数），改它的值不影响别处；值为 null、或实参是列与表达式时返回 <see langword="false"/>，由调用方在 SQL 里转义。
    /// </remarks>
    private static bool TryBindPattern(MethodCallExpressionModel model, MethodCallExpressionArgs argument, DbType dbType, bool leading, bool trailing, out string parameterName)
    {
        parameterName = string.Empty;
        if (argument.IsMember || argument.MemberName is not string name)
        {
            return false;
        }

        var parameter = model.Parameters?.Find(p => string.Equals(p.ParameterName, name, StringComparison.Ordinal));
        var value = parameter?.Value switch
        {
            string text => text,
            char character => character.ToString(),
            _ => null,
        };
        if (parameter is null || value is null)
        {
            return false;
        }

        if (parameter.Value is char)
        {
            // 字符实参建参数时按字符推断了类型，换成字符串值后改成字符串类型
            parameter.DbType = System.Data.DbType.String;
        }

        parameter.Value = $"{(leading ? "%" : string.Empty)}{Escape(value, dbType)}{(trailing ? "%" : string.Empty)}";
        parameterName = name;
        return true;
    }

    /// <summary>
    /// 列或表达式实参的 SQL 文本。
    /// </summary>
    private static string Operand(MethodCallExpressionArgs argument, string method)
        => argument.MemberName?.ToString() is { } sql && !string.IsNullOrWhiteSpace(sql)
            ? sql
            : throw new NotSupportedException($"The argument of '{method}' cannot be translated.");

    /// <summary>
    /// 在 SQL 里转义列或表达式的值。
    /// </summary>
    /// <remarks>
    /// 先替换转义符自己，再替换通配符，否则后加的转义符会被再转义一次。
    /// </remarks>
    private static string EscapeInSql(string operand, DbType dbType)
    {
        var escaped = $"REPLACE(REPLACE(REPLACE({operand}, '{EscapeCharacter}', '{EscapeCharacter}{EscapeCharacter}'), '%', '{EscapeCharacter}%'), '_', '{EscapeCharacter}_')";
        return dbType == DbType.SqlServer ? $"REPLACE({escaped}, '[', '{EscapeCharacter}[')" : escaped;
    }

    /// <summary>
    /// 按方言拼上前后的通配符。
    /// </summary>
    /// <remarks>
    /// 写法与 SqlSugar 原来的翻译一致：PostgreSQL 与 MySQL 用 <c>concat</c>，SQL Server 用 <c>+</c>，SQLite 与 Oracle 用 <c>||</c>。
    /// </remarks>
    private static string Concatenate(DbType dbType, bool leading, string operand, bool trailing)
    {
        var parts = new List<string>(3);
        if (leading)
        {
            parts.Add("'%'");
        }

        parts.Add(operand);
        if (trailing)
        {
            parts.Add("'%'");
        }

        return dbType switch
        {
            DbType.PostgreSQL or DbType.MySql => $"concat({string.Join(", ", parts)})",
            DbType.SqlServer => string.Join(" + ", parts),
            _ => string.Join(" || ", parts),
        };
    }
}

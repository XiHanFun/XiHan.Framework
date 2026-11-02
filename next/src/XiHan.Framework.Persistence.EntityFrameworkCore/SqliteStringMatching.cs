// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// SQLite 上字符串包含、前缀与后缀的统一翻译。
/// </summary>
/// <remarks>
/// EF Core 的 SQLite Provider 把 <see cref="string.Contains(string)"/> 译成区分大小写的 <c>instr</c>，<see cref="string.StartsWith(string)"/> / <see cref="string.EndsWith(string)"/> 的值实参译成只对 ASCII 字母不区分大小写的 <c>LIKE</c>、列实参译成区分的 <c>substr</c> 比较，同一个库上三种规则（RFC-0005「规约里的字符串匹配」，ADR-0101）。
/// <see cref="SqliteStringMatchingInterceptor"/> 在查询编译之前把这三个方法换成这里的标记方法，<see cref="SqliteStringMatchingTranslator"/> 把标记方法一律译成 <c>LIKE … ESCAPE '\'</c>，与 SqlSugar 在 SQLite 上同一套规则。
/// 标记方法的方法体只在投影落到客户端求值时执行，按 .NET 的原方法计算，与改写之前相同。
/// </remarks>
internal static class SqliteStringMatching
{
    /// <summary>
    /// 包含的标记方法。
    /// </summary>
    public static MethodInfo ContainsMethod { get; } = typeof(SqliteStringMatching).GetMethod(nameof(Contains), BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>
    /// 前缀的标记方法。
    /// </summary>
    public static MethodInfo StartsWithMethod { get; } = typeof(SqliteStringMatching).GetMethod(nameof(StartsWith), BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>
    /// 后缀的标记方法。
    /// </summary>
    public static MethodInfo EndsWithMethod { get; } = typeof(SqliteStringMatching).GetMethod(nameof(EndsWith), BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>
    /// 在 DbContext 选项上加上拦截器与翻译插件。
    /// </summary>
    /// <remarks>
    /// 特性每次构建选项时调用。拦截器只在 SQLite 上改写，翻译插件只认标记方法，其余 Provider 不受影响。
    /// </remarks>
    public static void Use(DbContextOptionsBuilder options)
    {
        options.AddInterceptors(SqliteStringMatchingInterceptor.Instance);
        ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new SqliteStringMatchingExtension());
    }

    /// <summary>
    /// 包含：客户端求值时同 <see cref="string.Contains(string)"/>（序数比较）。
    /// </summary>
    private static bool Contains(string value, string part) => value.Contains(part, StringComparison.Ordinal);

    /// <summary>
    /// 前缀：客户端求值时同 <see cref="string.StartsWith(string)"/>（当前文化）。
    /// </summary>
    private static bool StartsWith(string value, string part) => value.StartsWith(part, StringComparison.CurrentCulture);

    /// <summary>
    /// 后缀：客户端求值时同 <see cref="string.EndsWith(string)"/>（当前文化）。
    /// </summary>
    private static bool EndsWith(string value, string part) => value.EndsWith(part, StringComparison.CurrentCulture);
}

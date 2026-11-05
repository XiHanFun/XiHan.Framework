// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 让 PostgreSQL 会话固定 <c>standard_conforming_strings = on</c>
/// </summary>
/// <remarks>
/// SqlSugar 在多行写入、批量更新、按主键删除、<c>In</c> 与集合 <c>Contains</c> 里把字符串内联成 <c>'…'</c> 字面量，只把单引号写成两个（<c>ToSqlFilter</c>），这只在 <c>standard_conforming_strings = on</c>（PostgreSQL 9.1 起的缺省）时正确；库、角色或服务器把它设成 off 时，普通字面量里的反斜杠是转义符：<c>x\'y</c> 让语句不合法，<c>a\b</c>、<c>line\n</c> 被静默改成退格与换行，按主键的更新、删除与查找静默漏掉行。所以在 Npgsql 连接串的启动参数（<c>Options</c>）里追加 <c>-c standard_conforming_strings=on</c>：服务器在会话建立时施加它，优先于库、角色与配置文件里的缺省值，连接池复用连接时的重置（<c>DISCARD ALL</c>）也回到它。会话里再执行 <c>SET standard_conforming_strings = off</c> 的，仍会让 SqlSugar 的内联字面量出错
/// </remarks>
internal static partial class PostgreSqlStartupOptions
{
    /// <summary>
    /// Npgsql 连接串里启动参数的键
    /// </summary>
    private const string OptionsKey = "Options";

    /// <summary>
    /// 追加的启动参数
    /// </summary>
    private const string StandardConformingStringsOn = "-c standard_conforming_strings=on";

    /// <summary>
    /// 在连接串的启动参数里确保 <c>standard_conforming_strings</c> 是 on
    /// </summary>
    /// <remarks>
    /// 启动参数里没有这一项时追加，已有的其他参数保留；已经写成 on（<c>on</c>、<c>true</c>、<c>yes</c>、<c>1</c> 及其前缀）时原样返回；写成别的值是与 SqlSugar 冲突的显式配置，抛出而不是改写它
    /// </remarks>
    /// <exception cref="ArgumentException">启动参数把 <c>standard_conforming_strings</c> 设成了 on 以外的值</exception>
    public static string Enforce(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        // DbConnectionStringBuilder 是 NpgsqlConnectionStringBuilder 的基类，解析与引号规则相同，不必引用 Npgsql 的类型
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var options = builder.TryGetValue(OptionsKey, out var existing) ? Convert.ToString(existing, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        var settings = Setting().Matches(options);
        if (settings.Count > 0)
        {
            // 写了几次都要是 on：不按「最后一个生效」替应用挑，写成 off 的意图不被静默推翻
            foreach (Match setting in settings)
            {
                var value = setting.Groups["value"].Value;
                if (!IsOn(value))
                {
                    throw new ArgumentException($"The PostgreSQL connection string sets 'standard_conforming_strings' to '{value}' in '{OptionsKey}'; SqlSugar inlines string literals that are only correct when it is 'on', so remove the setting or set it to 'on'.", nameof(connectionString));
                }
            }

            return connectionString;
        }

        builder[OptionsKey] = options.Length == 0 ? StandardConformingStringsOn : $"{options} {StandardConformingStringsOn}";
        return builder.ConnectionString;
    }

    /// <summary>
    /// 布尔设置的值是否为真
    /// </summary>
    /// <remarks>
    /// 照 PostgreSQL 的布尔解析：<c>on</c>、<c>1</c>，以及 <c>true</c>、<c>yes</c> 的非空前缀，不区分大小写
    /// </remarks>
    private static bool IsOn(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower is "on" or "1" || (lower.Length > 0 && ("true".StartsWith(lower, StringComparison.Ordinal) || "yes".StartsWith(lower, StringComparison.Ordinal)));
    }

    /// <summary>
    /// 启动参数里的 <c>standard_conforming_strings</c> 设置
    /// </summary>
    /// <remarks>
    /// <c>-c name=value</c>、<c>-cname=value</c> 与 <c>--name=value</c> 三种写法，不区分大小写
    /// </remarks>
    [GeneratedRegex(@"(?:^|\s)(?:-c\s*|--)standard_conforming_strings\s*=\s*(?<value>\S*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Setting();
}

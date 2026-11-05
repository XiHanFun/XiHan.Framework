// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 分表实体的表名模板：认出物理表、推出逻辑名
/// </summary>
/// <remarks>
/// SqlSugar 的分表表名带 <c>{year}</c>、<c>{month}</c>、<c>{day}</c> 占位符，物理表把它们换成四位与两位数字（按月分表是当月第一天，如 <c>Order_{year}{month}{day}</c> → <c>Order_20261101</c>）。逻辑名是模板去掉占位符、去掉首尾的下划线。匹配不区分大小写：PostgreSQL 把不加引号的表名折成小写
/// </remarks>
internal sealed partial class SplitTableTemplate
{
    /// <summary>
    /// 物理表名的匹配
    /// </summary>
    private readonly Regex _pattern;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="template">实体映射出的表名模板</param>
    private SplitTableTemplate(string template)
    {
        Template = template;
        var logical = Placeholder().Replace(template, string.Empty).Trim('_');
        LogicalName = logical.Length > 0 ? logical : template;
        var builder = new StringBuilder("^");
        var last = 0;
        foreach (Match match in Placeholder().Matches(template))
        {
            builder.Append(Regex.Escape(template[last..match.Index]));
            builder.Append(string.Equals(match.Groups[1].Value, "year", StringComparison.OrdinalIgnoreCase) ? "[0-9]{4}" : "[0-9]{2}");
            last = match.Index + match.Length;
        }

        builder.Append(Regex.Escape(template[last..])).Append('$');
        _pattern = new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 表名模板
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// 逻辑名
    /// </summary>
    public string LogicalName { get; }

    /// <summary>
    /// 按实体映射出的表名建模板
    /// </summary>
    public static SplitTableTemplate Create(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        return new SplitTableTemplate(template);
    }

    /// <summary>
    /// 物理表名是否属于这个模板
    /// </summary>
    public bool Matches(string table) => _pattern.IsMatch(table);

    /// <summary>
    /// 模板与逻辑名
    /// </summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Template} -> {LogicalName}");

    /// <summary>
    /// 日期占位符的匹配
    /// </summary>
    [GeneratedRegex(@"\{(year|month|day)\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}

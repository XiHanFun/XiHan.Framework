// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 列表查询关键字的折叠与匹配规则
/// </summary>
/// <remarks>
/// 定义、实例与待办查询的 <c>Keyword</c> 都按这里匹配：关键字去掉首尾空白，空白视同未给；关键字与被搜的文本先折叠，再按序数判断包含（ADR-0096）。折叠是逐个码位的 Unicode 简单小写映射（ADR-0296），字段分隔符 U+001F 换成空格；别的字符原样保留，<c>%</c>、<c>_</c>、<c>[</c>、<c>\</c> 按字面匹配，与全角的 <c>％</c>、<c>＿</c>、<c>［</c>、<c>＼</c> 是不同的字符（ADR-0099 第四节撤销了第八批把它们换成全角的做法）。数据库实现把被搜文本的折叠结果存进查询列、在 SQL 里对它做 <c>Contains</c>：按字面由持久化保证（RFC-0005「规约里的字符串匹配」，SqlSugar 在五种主库上译成带转义的 <c>LIKE</c>，EF Core 按字面）；两边都折叠成小写，是因为 EF Core 的 <c>Contains</c> 与 PostgreSQL、Oracle 的 <c>LIKE</c> 区分大小写，SQLite 的 <c>LIKE</c> 只对 ASCII 不区分——不折叠，各 Provider 与内存实现的结果就不一致。数据库的排序规则另有放宽（如 MySQL 缺省排序规则不区分重音）时，以排序规则为准；持久化还没保证字面匹配的库（ADR-0098 的兼容库）上，<c>%</c>、<c>_</c> 照那个库的 <c>LIKE</c> 当通配
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowKeyword
{
    /// <summary>
    /// 多个字段折叠后拼进一列时的分隔符（U+001F）
    /// </summary>
    /// <remarks>
    /// 折叠把它换成空格，折叠后的关键字里不会有它，所以一次包含判断不会跨过字段的边界
    /// </remarks>
    public const char FieldSeparator = '\u001F';

    /// <summary>
    /// 规范化关键字：去掉首尾空白后折叠
    /// </summary>
    /// <returns>折叠后的关键字；为 <see langword="null"/>、空或只有空白时为 <see langword="null"/>（不过滤）</returns>
    public static string? Normalize(string? keyword)
    {
        var trimmed = keyword?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : Fold(trimmed);
    }

    /// <summary>
    /// 折叠一段文本
    /// </summary>
    /// <remarks>
    /// 逐个码位取 Unicode 的简单小写映射（UnicodeData.txt 的 Simple_Lowercase_Mapping），不做 SpecialCasing 的完全映射，也不随上下文与区域（RFC-0021「定义」的「关键字的小写」，ADR-0296）：<c>İ</c>（U+0130）折成 <c>i</c>，<c>Σ</c> 总是折成 <c>σ</c>，基本多文种平面以外的字母同样折叠，孤立的代理项不变；映射与表达式的 <c>lower</c> 是同一份（ADR-0303），结果以平台所带的 Unicode 数据为准。之后把字段分隔符换成空格。简单小写映射不跨平面，折叠前后的长度相同。幂等：折叠过的文本再折叠不变
    /// </remarks>
    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // 分隔符换成空格，关键字里就不会有它，一次包含判断不跨字段；按字面匹配交给持久化（ADR-0099 第四节）
        return WorkflowCaseMapping.ToLower(text).Replace(FieldSeparator, ' ');
    }

    /// <summary>
    /// 把若干字段各自折叠后以分隔符拼成一列的值
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 的字段按空串拼入，字段的位置不变。数据库实现用它写多字段的查询列
    /// </remarks>
    public static string Join(params ReadOnlySpan<string?> fields)
    {
        var folded = new string[fields.Length];
        for (var index = 0; index < fields.Length; index++)
        {
            folded[index] = fields[index] is { } field ? Fold(field) : string.Empty;
        }

        return string.Join(FieldSeparator, folded);
    }

    /// <summary>
    /// 任一字段折叠后是否包含规范化的关键字
    /// </summary>
    /// <remarks>
    /// <paramref name="keyword"/> 必须是 <see cref="Normalize"/> 的结果；为 <see langword="null"/> 的字段不匹配
    /// </remarks>
    public static bool Matches(string keyword, params ReadOnlySpan<string?> fields)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        foreach (var field in fields)
        {
            if (field is not null && Fold(field).Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

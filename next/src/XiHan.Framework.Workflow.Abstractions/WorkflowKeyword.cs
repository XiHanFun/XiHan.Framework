// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 列表查询关键字的折叠与匹配规则。
/// </summary>
/// <remarks>
/// 定义、实例与待办查询的 <c>Keyword</c> 都按这里匹配：关键字去掉首尾空白，空白视同未给；关键字与被搜的文本先折叠，再按序数判断包含（ADR-0096）。折叠是不变文化的小写，并把 SQL <c>LIKE</c> 的通配与转义字符 <c>%</c>、<c>_</c>、<c>[</c>、<c>\</c> 换成全角的 <c>％</c>、<c>＿</c>、<c>［</c>、<c>＼</c>，字段分隔符 U+001F 换成空格。数据库实现把被搜文本的折叠结果存进查询列，SQL 里比较的两边都不含通配字符，翻译成 <c>LIKE</c>（SqlSugar）还是 <c>instr</c> / <c>strpos</c>（EF Core）结果都与内存实现一致；代价是这四个 ASCII 字符与对应的全角字符视为相同。数据库的排序规则另有放宽（如 MySQL 缺省排序规则不区分重音）时，以排序规则为准。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowKeyword
{
    /// <summary>
    /// 多个字段折叠后拼进一列时的分隔符（U+001F）。
    /// </summary>
    /// <remarks>
    /// 折叠把它换成空格，折叠后的关键字里不会有它，所以一次包含判断不会跨过字段的边界。
    /// </remarks>
    public const char FieldSeparator = '\u001F';

    /// <summary>
    /// 规范化关键字：去掉首尾空白后折叠。
    /// </summary>
    /// <returns>折叠后的关键字；为 <see langword="null"/>、空或只有空白时为 <see langword="null"/>（不过滤）。</returns>
    public static string? Normalize(string? keyword)
    {
        var trimmed = keyword?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : Fold(trimmed);
    }

    /// <summary>
    /// 折叠一段文本。
    /// </summary>
    /// <remarks>
    /// 幂等：折叠过的文本再折叠不变。
    /// </remarks>
    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                // 先小写再换字符：全角的四个字符没有大小写，换出来的结果不受小写影响
                span[index] = char.ToLowerInvariant(source[index]) switch
                {
                    '%' => '％',
                    '_' => '＿',
                    '[' => '［',
                    '\\' => '＼',
                    FieldSeparator => ' ',
                    var other => other,
                };
            }
        });
    }

    /// <summary>
    /// 把若干字段各自折叠后以分隔符拼成一列的值。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 的字段按空串拼入，字段的位置不变。数据库实现用它写多字段的查询列。
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
    /// 任一字段折叠后是否包含规范化的关键字。
    /// </summary>
    /// <remarks>
    /// <paramref name="keyword"/> 必须是 <see cref="Normalize"/> 的结果；为 <see langword="null"/> 的字段不匹配。
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

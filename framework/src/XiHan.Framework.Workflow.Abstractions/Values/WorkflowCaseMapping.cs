// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// 逐个码位的 Unicode 简单大小写映射
/// </summary>
/// <remarks>
/// 列表查询关键字的折叠与表达式的 <c>lower</c> / <c>upper</c> 共用这一份（RFC-0021「定义」的「关键字的小写」与「表达式」的「内置函数」，ADR-0296、ADR-0303）：一个码位对一个码位，取 UnicodeData.txt 的 Simple_Lowercase_Mapping / Simple_Uppercase_Mapping，没有映射的码位不变；不做 SpecialCasing 的完全映射，也不随上下文与区域。映射取自平台所带的 Unicode 数据：整串交给不变文化转换（成批转换，比逐个字符快一个数量级），再补上它与简单映射分歧的地方——U+0130 的小写是 <c>i</c>、U+0131 的大写是 <c>I</c>（不变文化不转换它们），基本多文种平面以外的字母按整个码位经 <see cref="Rune"/> 转换。简单映射不跨平面，转换前后的 UTF-16 长度相同；孤立的代理项原样保留
/// </remarks>
internal static class WorkflowCaseMapping
{
    /// <summary>
    /// 带点的大写 I（U+0130）
    /// </summary>
    /// <remarks>
    /// 简单小写映射是 <c>i</c>（U+0069），不变文化不转换它
    /// </remarks>
    private const char DottedCapitalI = 'İ';

    /// <summary>
    /// 无点的小写 i（U+0131）
    /// </summary>
    /// <remarks>
    /// 简单大写映射是 <c>I</c>（U+0049），不变文化不转换它
    /// </remarks>
    private const char DotlessSmallI = 'ı';

    /// <summary>
    /// 要补的两个字符：整串转换之后在原文里找到它们才逐个修正
    /// </summary>
    private static readonly SearchValues<char> _dotted = SearchValues.Create([DottedCapitalI, DotlessSmallI]);

    /// <summary>
    /// 逐个码位取简单小写映射
    /// </summary>
    public static string ToLower(string text) => Map(text, upper: false);

    /// <summary>
    /// 逐个码位取简单大写映射
    /// </summary>
    public static string ToUpper(string text) => Map(text, upper: true);

    /// <summary>
    /// 逐个码位转换
    /// </summary>
    /// <param name="text">原文</param>
    /// <param name="upper">为真取大写映射，否则取小写映射</param>
    private static string Map(string text, bool upper)
    {
        ArgumentNullException.ThrowIfNull(text);
        var mapped = upper ? text.ToUpperInvariant() : text.ToLowerInvariant();
        if (mapped.Length != text.Length)
        {
            throw new InvalidOperationException("The invariant case mapping changed the length of the text.");
        }

        // 原文里没有两个带点的 i、也没有代理项时整串转换的结果就是简单映射：绝大多数文本走这一条
        var source = text.AsSpan();
        if (source.IndexOfAny(_dotted) < 0 && !source.ContainsAnyInRange('\uD800', '\uDFFF'))
        {
            return mapped;
        }

        return string.Create(text.Length, (text, mapped, upper), static (span, state) =>
        {
            state.mapped.AsSpan().CopyTo(span);
            var original = state.text;
            for (var index = 0; index < original.Length; index++)
            {
                var c = original[index];
                if (char.IsHighSurrogate(c) && index + 1 < original.Length && char.IsLowSurrogate(original[index + 1]))
                {
                    // 平面以外的字母按整个码位映射：不变文化在有的平台上逐个码元转换，会把它们原样留下
                    var rune = new Rune(c, original[index + 1]);
                    var result = state.upper ? Rune.ToUpperInvariant(rune) : Rune.ToLowerInvariant(rune);
                    if (result.EncodeToUtf16(span[index..]) != 2)
                    {
                        throw new InvalidOperationException($"The case mapping of U+{rune.Value:X} leaves the supplementary planes.");
                    }

                    index++;
                    continue;
                }

                if (!state.upper && c == DottedCapitalI)
                {
                    span[index] = 'i';
                }
                else if (state.upper && c == DotlessSmallI)
                {
                    span[index] = 'I';
                }
            }
        });
    }
}

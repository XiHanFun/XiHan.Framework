// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 消息键文法与模板格式化。消息键是专门的代码（如 <c>security.token.subject_missing</c>），各包在 <c>&lt;领域&gt;Messages</c> 常量类里定义，
/// 文案只存在于语言文件 <c>Localization/&lt;区域&gt;.json</c>（一种语言一个文件，含默认语言 <c>en-US</c>），由本地化器按调用方区域解析。
/// 模板语法：<c>{name}</c>（按名字）或 <c>{0}</c>（按位置）占位符从参数取值，可带 <c>:format</c> 格式说明符并按目标区域的文化格式化，<c>{{</c> 与 <c>}}</c> 是转义。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class MessageTemplate
{
    /// <summary>
    /// 消息键的最大长度。
    /// </summary>
    public const int MaxKeyLength = 128;

    /// <summary>
    /// 消息键是否合法：<c>^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$</c>，首段是领域词的小写形式。
    /// </summary>
    public static bool IsValidKey([NotNullWhen(true)] string? key)
        => key is not null && key.Length <= MaxKeyLength && KeyPattern().IsMatch(key);

    /// <summary>
    /// 校验消息键；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string ValidateKey(string key, string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(key, paramName ?? nameof(key));
        return IsValidKey(key)
            ? key
            : throw new ArgumentException($"Message key '{key}' is not valid; expected lowercase dot-separated segments such as 'persistence.entity.not_found'.", paramName ?? nameof(key));
    }

    /// <summary>
    /// 消息键的首段（领域）。
    /// </summary>
    public static string DomainOf(string key)
    {
        ValidateKey(key);
        return key[..key.IndexOf('.', StringComparison.Ordinal)];
    }

    /// <summary>
    /// 未解析时的开发者可读形式：键后跟参数列表，如 <c>persistence.entity.not_found (entity=Order, id=42)</c>。
    /// 没有本地化器或目录里没有该键时，<see cref="Error.Message"/> 与本地化器都退回到它，让键与参数在日志里可见。
    /// </summary>
    public static string Describe(string key, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (arguments is null || arguments.Count == 0)
        {
            return key;
        }

        var builder = new StringBuilder(key.Length + 16 * arguments.Count).Append(key).Append(" (");
        var first = true;
        foreach (var (name, value) in arguments)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            builder.Append(name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        return builder.Append(')').ToString();
    }

    /// <summary>
    /// 宽松格式化：缺值的占位符原样保留；<paramref name="provider"/> 为空时用不变文化。
    /// </summary>
    public static string Format(string text, IReadOnlyDictionary<string, object?>? arguments, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        Render(text, arguments, provider ?? CultureInfo.InvariantCulture, strict: false, out var result);
        return result!;
    }

    /// <summary>
    /// 严格格式化：任一占位符缺值返回 <see langword="false"/>，供本地化拒绝半截译文。
    /// </summary>
    public static bool TryFormat(string text, IReadOnlyDictionary<string, object?>? arguments, IFormatProvider? provider, [NotNullWhen(true)] out string? result)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Render(text, arguments, provider ?? CultureInfo.InvariantCulture, strict: true, out result);
    }

    /// <summary>
    /// 模板里的占位符名（不含格式说明符），按出现顺序去重。
    /// </summary>
    public static IReadOnlyList<string> PlaceholdersOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var names = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            ReadSegment(text, ref i, out var name, out _, out var literal);
            if (literal is null && !names.Contains(name!))
            {
                names.Add(name!);
            }
        }

        return names;
    }

    /// <summary>
    /// 逐段渲染模板：占位符取值并格式化（<see cref="IFormattable"/> 用格式说明符与文化，其余用 <see cref="Convert.ToString(object?, IFormatProvider?)"/>），转义还原为单个花括号。
    /// </summary>
    /// <returns>严格模式下任一占位符缺值返回 <see langword="false"/>。</returns>
    private static bool Render(string text, IReadOnlyDictionary<string, object?>? arguments, IFormatProvider provider, bool strict, out string? result)
    {
        var builder = new StringBuilder(text.Length + 16);
        var i = 0;
        while (i < text.Length)
        {
            ReadSegment(text, ref i, out var name, out var format, out var literal);
            if (literal is not null)
            {
                builder.Append(literal);
                continue;
            }

            if (arguments is null || !arguments.TryGetValue(name!, out var value))
            {
                // 缺值：严格模式整条拒绝；宽松模式把占位符原样留在文本里，让遗漏可见而不是悄悄吞掉
                if (strict)
                {
                    result = null;
                    return false;
                }

                builder.Append('{').Append(name).Append(format is null ? string.Empty : ":" + format).Append('}');
                continue;
            }

            builder.Append(value is IFormattable formattable ? formattable.ToString(format, provider) : Convert.ToString(value, provider));
        }

        result = builder.ToString();
        return true;
    }

    /// <summary>
    /// 从 <paramref name="index"/> 读取下一段：占位符（<paramref name="name"/> 与可选 <paramref name="format"/>）或字面量（<paramref name="literal"/>，含转义后的单个花括号与普通字符）。
    /// 没有闭合或名字不合法的花括号按字面量处理；<paramref name="index"/> 读完后前移。
    /// </summary>
    private static void ReadSegment(string text, ref int index, out string? name, out string? format, out string? literal)
    {
        name = null;
        format = null;
        var c = text[index];

        // "{{" 与 "}}" 是转义的单个花括号
        if ((c == '{' || c == '}') && index + 1 < text.Length && text[index + 1] == c)
        {
            literal = c.ToString();
            index += 2;
            return;
        }

        if (c != '{')
        {
            literal = c.ToString();
            index++;
            return;
        }

        var end = text.IndexOf('}', index + 1);
        if (end < 0)
        {
            // 没有闭合的左花括号按字面量输出
            literal = text[index..];
            index = text.Length;
            return;
        }

        var body = text[(index + 1)..end];
        var separator = body.IndexOf(':', StringComparison.Ordinal);
        var candidate = separator < 0 ? body : body[..separator];
        if (!PlaceholderName().IsMatch(candidate))
        {
            // 花括号里不是合法名字（如 JSON 片段）按字面量输出
            literal = text[index..(end + 1)];
            index = end + 1;
            return;
        }

        name = candidate;
        format = separator < 0 ? null : body[(separator + 1)..];
        literal = null;
        index = end + 1;
    }

    /// <summary>
    /// 消息键文法：小写字母开头的点分段，首段只含字母数字，其余段可含下划线。
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$")]
    private static partial Regex KeyPattern();

    /// <summary>
    /// 占位符名文法：字母开头的字母、数字与下划线（按名字），或纯数字（按位置）。
    /// </summary>
    [GeneratedRegex("^([A-Za-z][A-Za-z0-9_]*|[0-9]+)$")]
    private static partial Regex PlaceholderName();
}

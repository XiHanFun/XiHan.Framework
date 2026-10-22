// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.Kernel.Generators;

/// <summary>
/// 属性命名策略的实现，与 System.Text.Json 的内置策略一致。
/// </summary>
internal static class NamingPolicies
{
    /// <summary>
    /// 按策略转换属性名。
    /// </summary>
    public static string Apply(JsonNamingPolicyKind policy, string name)
        => policy switch
        {
            JsonNamingPolicyKind.CamelCase => CamelCase(name),
            JsonNamingPolicyKind.SnakeCaseLower => Separate(name, '_', upper: false),
            JsonNamingPolicyKind.SnakeCaseUpper => Separate(name, '_', upper: true),
            JsonNamingPolicyKind.KebabCaseLower => Separate(name, '-', upper: false),
            JsonNamingPolicyKind.KebabCaseUpper => Separate(name, '-', upper: true),
            _ => name,
        };

    /// <summary>
    /// camelCase：连续的前导大写字母一起转小写（<c>IOStream</c> → <c>ioStream</c>），与 STJ 一致。
    /// </summary>
    private static string CamelCase(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return name;
        }

        var characters = name.ToCharArray();
        for (var i = 0; i < characters.Length; i++)
        {
            // 下一个字符是小写时停止，保留后面单词的首字母（HTTPServer → httpServer）
            if (i > 0 && i + 1 < characters.Length && char.IsLower(characters[i + 1]))
            {
                break;
            }

            if (!char.IsUpper(characters[i]))
            {
                break;
            }

            characters[i] = char.ToLowerInvariant(characters[i]);
        }

        return new string(characters);
    }

    /// <summary>
    /// 按单词边界插入分隔符：小写后接大写、大写后接"大写+小写"、字母与数字之间都算边界。
    /// </summary>
    private static string Separate(string name, char separator, bool upper)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && IsBoundary(name, i))
            {
                builder.Append(separator);
            }

            builder.Append(upper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 位置 <paramref name="i"/> 之前是否是单词边界。
    /// </summary>
    private static bool IsBoundary(string name, int i)
    {
        var previous = name[i - 1];
        var current = name[i];
        if (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous)))
        {
            return true;
        }

        if (char.IsUpper(current) && char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]))
        {
            return true;
        }

        return char.IsDigit(current) && char.IsLetter(previous);
    }
}

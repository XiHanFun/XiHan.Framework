#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StringExtensions
// Guid:3630d8a8-77e0-45eb-a1e6-f9a6b5dc26ba
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-12-03 上午 12:30:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Utils.Security.Cryptography;
using XiHan.Framework.Utils.System;
using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.Text;

/// <summary>
/// 字符串扩展方法
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// 如果给定字符串不以该字符结尾，则在其末尾添加一个字符
    /// </summary>
    /// <param name="str"></param>
    /// <param name="c"></param>
    /// <param name="comparisonType"></param>
    /// <returns></returns>
    public static string EnsureEndsWith(this string str, char c, StringComparison comparisonType = StringComparison.Ordinal)
    {
        _ = CheckHelper.NotNull(str, nameof(str));

        return str.EndsWith(c.ToString(), comparisonType) ? str : str + c;
    }

    /// <summary>
    /// 如果给定字符串不以该字符开头，则在其开头添加一个字符
    /// </summary>
    /// <param name="str"></param>
    /// <param name="c"></param>
    /// <param name="comparisonType"></param>
    /// <returns></returns>
    public static string EnsureStartsWith(this string str, char c, StringComparison comparisonType = StringComparison.Ordinal)
    {
        _ = CheckHelper.NotNull(str, nameof(str));

        return str.StartsWith(c.ToString(), comparisonType) ? str : c + str;
    }

    /// <summary>
    /// 指示此字符串是否为空或一个 System.String.Empty 字符串
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? str)
    {
        return string.IsNullOrEmpty(str);
    }

    /// <summary>
    /// 指示此字符串是否为 null、为空，或者仅由空白字符组成
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? str)
    {
        return string.IsNullOrWhiteSpace(str);
    }

    /// <summary>
    /// 从字符串的开头获取该字符串的子字符串
    /// </summary>
    /// <exception cref="ArgumentNullException">如果 <paramref name="str"/> 为 null，则抛出</exception>
    /// <exception cref="ArgumentException">如果 <paramref name="len"/> 大于字符串的长度，则抛出</exception>
    public static string Left(this string str, int len)
    {
        _ = CheckHelper.NotNull(str, nameof(str));

        return str.Length < len ? throw new ArgumentException("len 参数不能大于给定字符串的长度！") : str[..len];
    }

    /// <summary>
    /// 将字符串中的行结尾转换为 <see cref="Environment.NewLine"/>
    /// </summary>
    public static string NormalizeLineEndings(this string str)
    {
        return str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
    }

    /// <summary>
    /// 获取字符串中字符的第 n 次出现的索引
    /// </summary>
    /// <param name="str">要搜索的源字符串</param>
    /// <param name="c">在 <paramref name="str"/> 中搜索的字符</param>
    /// <param name="n">出现次数</param>
    public static int NthIndexOf(this string str, char c, int n)
    {
        _ = CheckHelper.NotNull(str, nameof(str));

        var count = 0;
        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] != c)
            {
                continue;
            }

            if (++count == n)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 从给定字符串的末尾移除给定后缀的第一个出现
    /// </summary>
    /// <param name="str">字符串。</param>
    /// <param name="postFixes">一个或多个后缀。</param>
    /// <returns>修改后的字符串，或者如果它没有任何给定的后缀，则返回相同的字符串</returns>
    public static string RemovePostFix(this string str, params string[] postFixes)
    {
        return str.RemovePostFix(StringComparison.Ordinal, postFixes);
    }

    /// <summary>
    /// 从给定字符串的末尾移除给定后缀的首次出现
    /// </summary>
    /// <param name="str">字符串。</param>
    /// <param name="comparisonType">字符串比较类型</param>
    /// <param name="postFixes">一个或多个后缀。</param>
    /// <returns>如果没有给定的任何后缀，则返回修改后的字符串或相同的字符串</returns>
    public static string RemovePostFix(this string str, StringComparison comparisonType, params string[] postFixes)
    {
        if (str.IsNullOrEmpty())
        {
            return str;
        }

        if (postFixes.IsNullOrEmpty())
        {
            return str;
        }

        foreach (var postFix in postFixes)
        {
            if (str.EndsWith(postFix, comparisonType))
            {
                return str.Left(str.Length - postFix.Length);
            }
        }

        return str;
    }

    /// <summary>
    /// 从给定字符串的开头移除给定前缀的首次出现。
    /// </summary>
    /// <param name="str">字符串。</param>
    /// <param name="preFixes">一个或多个前缀。</param>
    /// <returns>如果没有给定的任何前缀，则返回修改后的字符串或相同的字符串</returns>
    public static string RemovePreFix(this string str, params string[] preFixes)
    {
        return str.RemovePreFix(StringComparison.Ordinal, preFixes);
    }

    /// <summary>
    /// 从给定字符串的开头移除给定前缀的首次出现。
    /// </summary>
    /// <param name="str">该字符串。</param>
    /// <param name="comparisonType">字符串比较类型。</param>
    /// <param name="preFixes">一个或多个前缀。</param>
    /// <returns>如果没有任何给定的前缀，则返回修改后的字符串或相同的字符串</returns>
    public static string RemovePreFix(this string str, StringComparison comparisonType, params string[] preFixes)
    {
        if (str.IsNullOrEmpty())
        {
            return str;
        }

        if (preFixes.IsNullOrEmpty())
        {
            return str;
        }

        foreach (var preFix in preFixes)
        {
            if (str.StartsWith(preFix, comparisonType))
            {
                return str.Right(str.Length - preFix.Length);
            }
        }

        return str;
    }

    /// <summary>
    /// 从字符串的开头移除给定前缀的所有出现。
    /// </summary>
    /// <param name="str"></param>
    /// <param name="search"></param>
    /// <param name="replace"></param>
    /// <param name="comparisonType"></param>
    /// <returns></returns>
    public static string ReplaceFirst(this string str, string search, string replace, StringComparison comparisonType = StringComparison.Ordinal)
    {
        _ = CheckHelper.NotNull(str, nameof(str));

        var pos = str.IndexOf(search, comparisonType);
        if (pos < 0)
        {
            return str;
        }

        var searchLength = search.Length;
        var replaceLength = replace.Length;
        var newLength = str.Length - searchLength + replaceLength;

        var buffer = newLength <= 1024 ? stackalloc char[newLength] : new char[newLength];

        // Copy the part of the original string before the search term
        str.AsSpan(0, pos).CopyTo(buffer);

        // Copy the replacement text
        replace.AsSpan().CopyTo(buffer[pos..]);

        // Copy the remainder of the original string
        str.AsSpan(pos + searchLength).CopyTo(buffer[(pos + replaceLength)..]);

        return buffer.ToString();
    }

    /// <summary>
    /// 从字符串的末尾获取该字符串的子字符串。
    /// </summary>
    /// <exception cref="ArgumentNullException">如果 <paramref name="str"/> 为 null，则抛出</exception>
    /// <exception cref="ArgumentException">如果 <paramref name="len"/> 大于字符串的长度，则抛出</exception>
    public static string Right(this string str, int len)
    {
        _ = CheckHelper.NotNull(str, nameof(str));

        return str.Length < len
            ? throw new ArgumentException("len argument can not be bigger than given string's length!")
            : str.Substring(str.Length - len, len);
    }

    /// <summary>
    /// 使用字符串的 Split 方法按给定分隔符拆分给定字符串。
    /// </summary>
    public static string[] Split(this string str, string separator)
    {
        return str.Split([
            separator
        ], StringSplitOptions.None);
    }

    /// <summary>
    /// 使用字符串的 Split 方法按给定分隔符拆分给定字符串。
    /// </summary>
    public static string[] Split(this string str, string separator, StringSplitOptions options)
    {
        return str.Split([
            separator
        ], options);
    }

    /// <summary>
    /// 使用字符串的 Split 方法按 <see cref="Environment.NewLine"/> 拆分给定字符串。
    /// </summary>
    public static string[] SplitToLines(this string str)
    {
        return str.Split(Environment.NewLine);
    }

    /// <summary>
    /// 使用字符串的“Split”方法，根据 <see cref="Environment.NewLine"/> 来拆分给定的字符串。
    /// </summary>
    public static string[] SplitToLines(this string str, StringSplitOptions options)
    {
        return str.Split(Environment.NewLine, options);
    }

    /// <summary>
    /// 将给定的帕斯卡格式/驼峰格式字符串转换为句子（通过按空格分隔单词）。
    /// 示例:“ThisIsSampleSentence”被转换为“ This is a sample sentence”。
    /// </summary>
    /// <param name="str">要转换的字符串。</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化。否则，将使用不变文化。</param>
    public static string ToSentenceCase(this string str, bool useCurrentCulture = false)
    {
        return string.IsNullOrWhiteSpace(str)
            ? str
            : useCurrentCulture
                ? RegexHelper.LetterRegex().Replace(str, m => m.Value[0] + " " + char.ToLower(m.Value[1]))
                : RegexHelper.LetterRegex().Replace(str, m => m.Value[0] + " " + char.ToLowerInvariant(m.Value[1]));
    }

    /// <summary>
    /// 将帕斯卡格式的字符串转换为驼峰格式的字符串。
    /// 例如:“ThisIsSampleSentence”被转换为“thisIsSampleSentence”。
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化。否则，将使用不变文化。</param>
    /// <param name="handleAbbreviations">如果您希望将 'XYZ' 转换为 'xyz'，则设置为 true。</param>
    /// <returns>该字符串的驼峰格式</returns>
    public static string ToCamelCase(this string str, bool useCurrentCulture = false, bool handleAbbreviations = false)
    {
        return string.IsNullOrWhiteSpace(str)
            ? str
            : str.Length == 1
                ? useCurrentCulture ? str.ToLower() : str.ToLowerInvariant()
                : handleAbbreviations && IsAllUpperCase(str)
                    ? useCurrentCulture ? str.ToLower() : str.ToLowerInvariant()
                    : (useCurrentCulture ? char.ToLower(str[0]) : char.ToLowerInvariant(str[0])) + str[1..];
    }

    /// <summary>
    /// 将给定的帕斯卡格式/驼峰格式字符串转换为短横线连接格式。
    /// 例如:“ThisIsSampleSentence”被转换为“this-is-a-sample-sentence”。
    /// </summary>
    /// <param name="str">要转换的字符串。</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化。否则，将使用不变文化。</param>
    public static string ToKebabCase(this string str, bool useCurrentCulture = false)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return str;
        }

        str = str.ToCamelCase();

        return useCurrentCulture
            ? RegexHelper.LetterRegex().Replace(str, m => m.Value[0] + "-" + char.ToLower(m.Value[1]))
            : RegexHelper.LetterRegex().Replace(str, m => m.Value[0] + "-" + char.ToLowerInvariant(m.Value[1]));
    }

    /// <summary>
    /// 将驼峰式字符串转换为帕斯卡式字符串。
    /// 例如"thisIsSampleSentence" 被转换为 "ThisIsSampleSentence"。
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化。否则，将使用不变文化。</param>
    /// <returns>该字符串的帕斯卡式</returns>
    public static string ToPascalCase(this string str, bool useCurrentCulture = false)
    {
        return string.IsNullOrWhiteSpace(str)
            ? str
            : str.Length == 1
                ? useCurrentCulture ? str.ToUpper() : str.ToUpperInvariant()
                : (useCurrentCulture ? char.ToUpper(str[0]) : char.ToUpperInvariant(str[0])) + str[1..];
    }

    /// <summary>
    /// 将给定的帕斯卡格式/驼峰格式字符串转换为蛇形格式。
    /// 例如:“ThisIsSampleSentence”被转换为“this_is_a_sample_sentence”。
    /// </summary>
    /// <param name="str">要转换的字符串。</param>
    /// <returns></returns>
    public static string ToSnakeCase(this string str)
    {
        return str.IsNullOrWhiteSpace() ? str : JsonNamingPolicy.SnakeCaseLower.ConvertName(str);
    }

    /// <summary>
    /// 将字符串转换为枚举值。
    /// </summary>
    /// <typeparam name="T">枚举的类型</typeparam>
    /// <param name="value">要转换的字符串值</param>
    /// <returns>返回枚举对象</returns>
    public static T ToEnum<T>(this string value)
        where T : struct
    {
        _ = CheckHelper.NotNull(value, nameof(value));
        return Enum.Parse<T>(value);
    }

    /// <summary>
    /// 将字符串转换为枚举值。
    /// </summary>
    /// <typeparam name="T">枚举的类型</typeparam>
    /// <param name="value">要转换的字符串值</param>
    /// <param name="ignoreCase">忽略大小写</param>
    /// <returns>返回枚举对象</returns>
    public static T ToEnum<T>(this string value, bool ignoreCase)
        where T : struct
    {
        _ = CheckHelper.NotNull(value, nameof(value));
        return Enum.Parse<T>(value, ignoreCase);
    }

    /// <summary>
    /// 将字符串转换为 MD5
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string ToMd5(this string str)
    {
        return HashHelper.Md5(str);
    }

    /// <summary>
    /// 如果字符串超过最大长度，则从字符串的开头获取该字符串的子字符串。
    /// </summary>
    public static string? Truncate(this string? str, int maxLength)
    {
        return str is null ? null : str.Length <= maxLength ? str : str.Left(maxLength);
    }

    /// <summary>
    /// 如果字符串超过最大长度，则从字符串的结尾获取该字符串的子字符串。
    /// </summary>
    public static string? TruncateFromBeginning(this string? str, int maxLength)
    {
        return str is null ? null : str.Length <= maxLength ? str : str.Right(maxLength);
    }

    /// <summary>
    /// 如果字符串超过最大长度，则从字符串的开头获取该字符串的子字符串。如果被截断，它会将给定的 <paramref name="postfix"/> 添加到字符串的末尾。
    /// 返回的字符串不能长于最大长度。
    /// </summary>
    /// <exception cref="ArgumentNullException">如果 <paramref name="str"/> 为 null，则抛出</exception>
    public static string? TruncateWithPostfix(this string? str, int maxLength, string postfix = "...")
    {
        return str is null
            ? null
            : str == string.Empty || maxLength == 0
                ? string.Empty
                : str.Length <= maxLength
                    ? str
                    : maxLength <= postfix.Length
                        ? postfix.Left(maxLength)
                        : str.Left(maxLength - postfix.Length) + postfix;
    }

    /// <summary>
    /// 使用 <see cref="Encoding.UTF8"/> 编码将给定字符串转换为字节数组。
    /// </summary>
    public static byte[] GetBytes(this string str)
    {
        return str.GetBytes(Encoding.UTF8);
    }

    /// <summary>
    /// 使用给定的 <paramref name="encoding"/> 将给定字符串转换为字节数组
    /// </summary>
    public static byte[] GetBytes(this string str, Encoding encoding)
    {
        _ = CheckHelper.NotNull(str, nameof(str));
        _ = CheckHelper.NotNull(encoding, nameof(encoding));

        return encoding.GetBytes(str);
    }

    /// <summary>
    /// 判断字符串是否全为大写
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private static bool IsAllUpperCase(string input)
    {
        return input.All(t => !char.IsLetter(t) || char.IsUpper(t));
    }
}

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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.Extensions;

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
        ArgumentNullException.ThrowIfNull(str, nameof(str));

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
        ArgumentNullException.ThrowIfNull(str, nameof(str));

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
        ArgumentNullException.ThrowIfNull(str, nameof(str));

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
        ArgumentNullException.ThrowIfNull(str, nameof(str));

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
    /// <param name="str">字符串</param>
    /// <param name="postFixes">一个或多个后缀</param>
    /// <returns>修改后的字符串，或者如果它没有任何给定的后缀，则返回相同的字符串</returns>
    public static string RemovePostFix(this string str, params string[] postFixes)
    {
        return str.RemovePostFix(StringComparison.Ordinal, postFixes);
    }

    /// <summary>
    /// 从给定字符串的末尾移除给定后缀的首次出现
    /// </summary>
    /// <param name="str">字符串</param>
    /// <param name="comparisonType">字符串比较类型</param>
    /// <param name="postFixes">一个或多个后缀</param>
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
    /// 从给定字符串的开头移除给定前缀的首次出现
    /// </summary>
    /// <param name="str">字符串</param>
    /// <param name="preFixes">一个或多个前缀</param>
    /// <returns>如果没有给定的任何前缀，则返回修改后的字符串或相同的字符串</returns>
    public static string RemovePreFix(this string str, params string[] preFixes)
    {
        return str.RemovePreFix(StringComparison.Ordinal, preFixes);
    }

    /// <summary>
    /// 从给定字符串的开头移除给定前缀的首次出现
    /// </summary>
    /// <param name="str">该字符串</param>
    /// <param name="comparisonType">字符串比较类型</param>
    /// <param name="preFixes">一个或多个前缀</param>
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
    /// 从字符串的开头移除给定前缀的所有出现
    /// </summary>
    /// <param name="str"></param>
    /// <param name="search"></param>
    /// <param name="replace"></param>
    /// <param name="comparisonType"></param>
    /// <returns></returns>
    public static string ReplaceFirst(this string str, string search, string replace, StringComparison comparisonType = StringComparison.Ordinal)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        var pos = str.IndexOf(search, comparisonType);
        if (pos < 0)
        {
            return str;
        }

        var searchLength = search.Length;
        var replaceLength = replace.Length;
        var newLength = str.Length - searchLength + replaceLength;

        var buffer = newLength <= 1024 ? stackalloc char[newLength] : new char[newLength];

        // 复制搜索词前的原始字符串部分
        str.AsSpan(0, pos).CopyTo(buffer);

        // 复制替换文本
        replace.AsSpan().CopyTo(buffer[pos..]);

        // 复制原始字符串的剩余部分
        str.AsSpan(pos + searchLength).CopyTo(buffer[(pos + replaceLength)..]);

        return buffer.ToString();
    }

    /// <summary>
    /// 从字符串的末尾获取该字符串的子字符串
    /// </summary>
    /// <exception cref="ArgumentNullException">如果 <paramref name="str"/> 为 null，则抛出</exception>
    /// <exception cref="ArgumentException">如果 <paramref name="len"/> 大于字符串的长度，则抛出</exception>
    public static string Right(this string str, int len)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        return str.Length < len
            ? throw new ArgumentException("len argument can not be bigger than given string's length!")
            : str.Substring(str.Length - len, len);
    }

    /// <summary>
    /// 使用字符串的 Split 方法按给定分隔符拆分给定字符串
    /// </summary>
    public static string[] Split(this string str, string separator)
    {
        return str.Split([
            separator
        ], StringSplitOptions.None);
    }

    /// <summary>
    /// 使用字符串的 Split 方法按给定分隔符拆分给定字符串
    /// </summary>
    public static string[] Split(this string str, string separator, StringSplitOptions options)
    {
        return str.Split([
            separator
        ], options);
    }

    /// <summary>
    /// 使用字符串的 Split 方法按 <see cref="Environment.NewLine"/> 拆分给定字符串
    /// </summary>
    public static string[] SplitToLines(this string str)
    {
        return str.Split(Environment.NewLine);
    }

    /// <summary>
    /// 使用字符串的"Split"方法，根据 <see cref="Environment.NewLine"/> 来拆分给定的字符串
    /// </summary>
    public static string[] SplitToLines(this string str, StringSplitOptions options)
    {
        return str.Split(Environment.NewLine, options);
    }

    /// <summary>
    /// 将给定的帕斯卡格式/驼峰格式字符串转换为句子(通过按空格分隔单词)
    /// 示例:ThisIsSampleSentence被转换为 This is a sample sentence
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化否则，将使用不变文化</param>
    public static string ToSentenceCase(this string str, bool useCurrentCulture = false)
    {
        return string.IsNullOrWhiteSpace(str)
            ? str
            : useCurrentCulture
                ? RegexHelper.LetterRegex().Replace(str, m => m.Value[0] + " " + char.ToLower(m.Value[1]))
                : RegexHelper.LetterRegex().Replace(str, m => m.Value[0] + " " + char.ToLowerInvariant(m.Value[1]));
    }

    /// <summary>
    /// 将帕斯卡格式的字符串转换为小驼峰格式的字符串
    /// 例如:ThisIsSampleSentence被转换为thisIsSampleSentence
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化否则，将使用不变文化</param>
    /// <param name="handleAbbreviations">如果您希望将 'XYZ' 转换为 'xyz'，则设置为 true</param>
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
    /// 将给定的帕斯卡格式/驼峰格式字符串转换为短横线连接格式
    /// 例如:ThisIsSampleSentence被转换为this-is-a-sample-sentence
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化否则，将使用不变文化</param>
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
    /// 将小驼峰式字符串转换为帕斯卡式字符串
    /// 例如:thisIsSampleSentence 被转换为 ThisIsSampleSentence
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <param name="useCurrentCulture">设置为 true 以使用当前文化否则，将使用不变文化</param>
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
    /// 将给定的帕斯卡格式/驼峰格式字符串转换为蛇形格式
    /// 例如:ThisIsSampleSentence 被转换为 this_is_a_sample_sentence
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <returns></returns>
    public static string ToSnakeCase(this string str)
    {
        return str.IsNullOrWhiteSpace() ? str : JsonNamingPolicy.SnakeCaseLower.ConvertName(str);
    }

    /// <summary>
    /// 将字符串转换为枚举值
    /// </summary>
    /// <typeparam name="T">枚举的类型</typeparam>
    /// <param name="value">要转换的字符串值</param>
    /// <returns>返回枚举对象</returns>
    public static T ToEnum<T>(this string value)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        return Enum.Parse<T>(value);
    }

    /// <summary>
    /// 将字符串转换为枚举值
    /// </summary>
    /// <typeparam name="T">枚举的类型</typeparam>
    /// <param name="value">要转换的字符串值</param>
    /// <param name="ignoreCase">忽略大小写</param>
    /// <returns>返回枚举对象</returns>
    public static T ToEnum<T>(this string value, bool ignoreCase)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        return Enum.Parse<T>(value, ignoreCase);
    }

    /// <summary>
    /// 将字符串转换为 MD5
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string ToMd5(this string str)
    {
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(str));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 如果字符串超过最大长度，则从字符串的开头获取该字符串的子字符串
    /// </summary>
    public static string? Truncate(this string? str, int maxLength)
    {
        return str is null ? null : str.Length <= maxLength ? str : str.Left(maxLength);
    }

    /// <summary>
    /// 如果字符串超过最大长度，则从字符串的结尾获取该字符串的子字符串
    /// </summary>
    public static string? TruncateFromBeginning(this string? str, int maxLength)
    {
        return str is null ? null : str.Length <= maxLength ? str : str.Right(maxLength);
    }

    /// <summary>
    /// 如果字符串超过最大长度，则从字符串的开头获取该字符串的子字符串如果被截断，它会将给定的 <paramref name="postfix"/> 添加到字符串的末尾
    /// 返回的字符串不能长于最大长度
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
    /// 使用 <see cref="Encoding.UTF8"/> 编码将给定字符串转换为字节数组
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
        ArgumentNullException.ThrowIfNull(str, nameof(str));
        ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));

        return encoding.GetBytes(str);
    }

    /// <summary>
    /// 检查字符串是否为有效的 JSON
    /// </summary>
    /// <param name="str">要检查的字符串</param>
    /// <returns>如果是有效的 JSON 则返回 true，否则返回 false</returns>
    public static bool IsValidJson(this string str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(str);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 比较两个字符串是否相等(忽略大小写)
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <param name="value">要比较的字符串</param>
    /// <returns>如果两个字符串相等(忽略大小写)则返回 true，否则返回 false</returns>
    public static bool EqualsIgnoreCase(this string? str, string? value)
    {
        return string.Equals(str, value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 将字符串的第一个字符转换为大写
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <returns>首字母大写的字符串</returns>
    public static string FirstCharToUpper(this string str)
    {
        return string.IsNullOrEmpty(str) || char.IsUpper(str[0]) ? str : char.ToUpperInvariant(str[0]) + str[1..];
    }

    /// <summary>
    /// 将字符串的第一个字符转换为小写
    /// </summary>
    /// <param name="str">要转换的字符串</param>
    /// <returns>首字母小写的字符串</returns>
    public static string FirstCharToLower(this string str)
    {
        return string.IsNullOrEmpty(str) || char.IsLower(str[0]) ? str : char.ToLowerInvariant(str[0]) + str[1..];
    }

    /// <summary>
    /// 检查字符串是否包含指定集合中的任意字符串
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <param name="values">要检查的字符串集合</param>
    /// <param name="comparisonType">字符串比较类型</param>
    /// <returns>如果包含任意一个指定的字符串则返回 true，否则返回 false</returns>
    public static bool ContainsAny(this string str, IEnumerable<string> values, StringComparison comparisonType = StringComparison.Ordinal)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));
        ArgumentNullException.ThrowIfNull(values, nameof(values));

        foreach (var value in values)
        {
            if (str.Contains(value, comparisonType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查字符串是否包含指定集合中的所有字符串
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <param name="values">要检查的字符串集合</param>
    /// <param name="comparisonType">字符串比较类型</param>
    /// <returns>如果包含所有指定的字符串则返回 true，否则返回 false</returns>
    public static bool ContainsAll(this string str, IEnumerable<string> values, StringComparison comparisonType = StringComparison.Ordinal)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));
        ArgumentNullException.ThrowIfNull(values, nameof(values));

        foreach (var value in values)
        {
            if (!str.Contains(value, comparisonType))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 将字符串分割成指定长度的部分
    /// </summary>
    /// <param name="str">要分割的字符串</param>
    /// <param name="partLength">每部分的长度</param>
    /// <returns>分割后的字符串数组</returns>
    public static IEnumerable<string> SplitInParts(this string str, int partLength)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        if (partLength <= 0)
        {
            throw new ArgumentException("部分长度必须大于零", nameof(partLength));
        }

        for (var i = 0; i < str.Length; i += partLength)
        {
            yield return str.Substring(i, Math.Min(partLength, str.Length - i));
        }
    }

    /// <summary>
    /// 使用 ReadOnlySpan 高效地检查字符串是否包含指定字符
    /// </summary>
    /// <param name="str">要搜索的字符串</param>
    /// <param name="value">要查找的字符</param>
    /// <returns>如果找到字符，则为 true；否则为 false</returns>
    public static bool Contains(this string str, char value)
    {
        return str.AsSpan().Contains(value);
    }

    /// <summary>
    /// 格式化字符串，与 string.Format 类似
    /// </summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化后的字符串</returns>

    public static string Format(this string format, params object[] args)
    {
        return string.Format(format, args);
    }

    /// <summary>
    /// 检查字符串是否为空或只包含空白字符，如果是则返回默认值
    /// </summary>
    /// <param name="str">要检查的字符串</param>
    /// <param name="defaultValue">如果字符串为空或空白，则返回的默认值</param>
    /// <returns>原始字符串或默认值</returns>
    public static string? DefaultIfNullOrWhiteSpace(this string? str, string? defaultValue)
    {
        return string.IsNullOrWhiteSpace(str) ? defaultValue : str;
    }

    /// <summary>
    /// 检查字符串是否为空或空白字符串，如果不是则执行指定操作
    /// </summary>
    /// <param name="str">要检查的字符串</param>
    /// <param name="action">如果字符串不为空或空白，则执行的操作</param>
    public static void IfNotNullOrWhiteSpace(this string? str, Action<string> action)
    {
        if (!string.IsNullOrWhiteSpace(str))
        {
            action(str);
        }
    }

    /// <summary>
    /// 获取字符串内容的字节大小
    /// </summary>
    /// <param name="str">要计算大小的字符串</param>
    /// <param name="encoding">使用的编码，默认为 UTF8</param>
    /// <returns>字符串的字节大小</returns>
    public static int GetByteSize(this string str, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        encoding ??= Encoding.UTF8;
        return encoding.GetByteCount(str);
    }

    /// <summary>
    /// 移除字符串中的所有空白字符
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <returns>移除空白字符后的字符串</returns>
    public static string RemoveWhiteSpaces(this string str)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        // 预先计算移除空白后的长度以优化内存分配
        var resultLength = 0;
        foreach (var c in str)
        {
            if (!char.IsWhiteSpace(c))
            {
                resultLength++;
            }
        }

        if (resultLength == str.Length)
        {
            return str;
        }

        var result = new char[resultLength];
        var index = 0;

        foreach (var c in str)
        {
            if (!char.IsWhiteSpace(c))
            {
                result[index++] = c;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// 从字符串中移除不可见字符(控制字符和空白字符)
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <returns>移除不可见字符后的字符串</returns>
    public static string RemoveInvisibleChars(this string str)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        // 预计算移除后长度
        var resultLength = 0;
        foreach (var c in str)
        {
            if (!char.IsControl(c) && !char.IsWhiteSpace(c))
            {
                resultLength++;
            }
        }

        if (resultLength == str.Length)
        {
            return str;
        }

        var result = new char[resultLength];
        var index = 0;

        foreach (var c in str)
        {
            if (!char.IsControl(c) && !char.IsWhiteSpace(c))
            {
                result[index++] = c;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// 安全地截取字符串，避免因索引超出范围而抛出异常
    /// </summary>
    /// <param name="str">源字符串</param>
    /// <param name="startIndex">起始索引</param>
    /// <param name="length">长度</param>
    /// <returns>截取的子字符串</returns>
    public static string SafeSubstring(this string str, int startIndex, int? length = null)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        if (startIndex < 0)
        {
            startIndex = 0;
        }

        if (startIndex >= str.Length)
        {
            return string.Empty;
        }

        if (length == null)
        {
            return str[startIndex..];
        }

        var realLength = length.Value;
        if (realLength < 0)
        {
            realLength = 0;
        }

        if (startIndex + realLength > str.Length)
        {
            realLength = str.Length - startIndex;
        }

        return str.Substring(startIndex, realLength);
    }

    /// <summary>
    /// 计算两个字符串之间的 Levenshtein 距离(编辑距离)
    /// </summary>
    /// <param name="str">第一个字符串</param>
    /// <param name="other">第二个字符串</param>
    /// <returns>表示两个字符串之间的编辑距离</returns>
    public static int LevenshteinDistance(this string str, string other)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var n = str.Length;
        var m = other.Length;
        var d = new int[n + 1, m + 1];

        // 第一列初始化
        for (var i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        // 第一行初始化
        for (var j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        // 计算编辑距离
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = other[j - 1] == str[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    /// <summary>
    /// 检查字符串是否包含中文字符
    /// </summary>
    /// <param name="str">要检查的字符串</param>
    /// <returns>如果包含中文字符则返回 true，否则返回 false</returns>
    public static bool ContainsChinese(this string str)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        return str.Any(c => c is >= (char)0x4E00 and <= (char)0x9FFF);
    }

    /// <summary>
    /// 反转字符串中的字符顺序
    /// </summary>
    /// <param name="str">要反转的字符串</param>
    /// <returns>反转后的字符串</returns>
    public static string Reverse(this string str)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        if (str.Length <= 1)
        {
            return str;
        }

        var charArray = str.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

    /// <summary>
    /// 确保字符串是指定长度，不足则用指定字符填充
    /// </summary>
    /// <param name="str">要处理的字符串</param>
    /// <param name="length">目标长度</param>
    /// <param name="padChar">填充字符</param>
    /// <param name="padLeft">是否左填充</param>
    /// <returns>处理后的字符串</returns>
    public static string PadToLength(this string str, int length, char padChar = ' ', bool padLeft = false)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        return str.Length >= length ? str : padLeft ? str.PadLeft(length, padChar) : str.PadRight(length, padChar);
    }

    /// <summary>
    /// 将字符串重复指定次数
    /// </summary>
    /// <param name="str">要重复的字符串</param>
    /// <param name="count">重复次数</param>
    /// <returns>重复后的字符串</returns>
    public static string Repeat(this string str, int count)
    {
        ArgumentNullException.ThrowIfNull(str, nameof(str));

        return count <= 0 ? string.Empty : count == 1 || str.Length == 0 ? str : string.Concat(Enumerable.Repeat(str, count));
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

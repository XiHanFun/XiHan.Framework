// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 字符串扩展方法测试
/// </summary>
/// <remarks>
/// Split 与 Contains(char) 被 string 自带的同签名实例方法遮蔽，只能走静态调用才测得到扩展实现。
/// 不覆盖 ToSentenceCase/ToKebabCase，原因见交付报告的疑似缺陷段落：它们用的正则是整串锚定的，
/// 与方法名宣称的"按驼峰切词"语义完全不符。
/// </remarks>
public class StringExtensionsTests
{
    /// <summary>
    /// 缺少结尾字符时补上，已有时保持不变
    /// </summary>
    [Fact]
    public void EnsureEndsWith_AppendsOnlyWhenMissing()
    {
        Assert.Equal("dir/", "dir".EnsureEndsWith('/'));
        Assert.Equal("dir/", "dir/".EnsureEndsWith('/'));
        Assert.Equal("/", string.Empty.EnsureEndsWith('/'));
    }

    /// <summary>
    /// 缺少起始字符时补上，已有时保持不变
    /// </summary>
    [Fact]
    public void EnsureStartsWith_PrependsOnlyWhenMissing()
    {
        Assert.Equal("/path", "path".EnsureStartsWith('/'));
        Assert.Equal("/path", "/path".EnsureStartsWith('/'));
    }

    /// <summary>
    /// 空判断覆盖 null、空串与纯空白
    /// </summary>
    [Fact]
    public void IsNullOrEmptyAndIsNullOrWhiteSpace_DifferOnWhitespace()
    {
        string? nothing = null;

        Assert.True(nothing.IsNullOrEmpty());
        Assert.True(string.Empty.IsNullOrEmpty());
        Assert.False("  ".IsNullOrEmpty());
        Assert.False("a".IsNullOrEmpty());

        Assert.True(nothing.IsNullOrWhiteSpace());
        Assert.True(string.Empty.IsNullOrWhiteSpace());
        Assert.True("  ".IsNullOrWhiteSpace());
        Assert.False("a".IsNullOrWhiteSpace());
    }

    /// <summary>
    /// 取左侧子串，长度超出时抛参数异常
    /// </summary>
    [Fact]
    public void Left_TakesPrefixOrThrows()
    {
        Assert.Equal("abc", "abcdef".Left(3));
        Assert.Equal("abc", "abc".Left(3));
        Assert.Equal(string.Empty, "abc".Left(0));
        Assert.Throws<ArgumentException>(() => "abc".Left(4));
        Assert.Throws<ArgumentNullException>(() => StringExtensions.Left(null!, 1));
    }

    /// <summary>
    /// 取右侧子串，长度超出时抛参数异常
    /// </summary>
    [Fact]
    public void Right_TakesSuffixOrThrows()
    {
        Assert.Equal("ef", "abcdef".Right(2));
        Assert.Equal(string.Empty, "abc".Right(0));
        Assert.Throws<ArgumentException>(() => "abc".Right(4));
        Assert.Throws<ArgumentNullException>(() => StringExtensions.Right(null!, 1));
    }

    /// <summary>
    /// 行尾统一为当前平台换行符
    /// </summary>
    [Fact]
    public void NormalizeLineEndings_UnifiesAllLineBreaks()
    {
        var input = "a\r\nb\rc\nd";
        var expected = string.Join(Environment.NewLine, "a", "b", "c", "d");

        Assert.Equal(expected, input.NormalizeLineEndings());
    }

    /// <summary>
    /// 取字符第 n 次出现的下标，不足次数时返回 -1
    /// </summary>
    [Fact]
    public void NthIndexOf_ReturnsNthOccurrenceOrMinusOne()
    {
        Assert.Equal(1, "a.b.c.d".NthIndexOf('.', 1));
        Assert.Equal(3, "a.b.c.d".NthIndexOf('.', 2));
        Assert.Equal(-1, "a.b.c.d".NthIndexOf('.', 9));
        Assert.Equal(-1, "abc".NthIndexOf('.', 1));
        Assert.Throws<ArgumentNullException>(() => StringExtensions.NthIndexOf(null!, '.', 1));
    }

    /// <summary>
    /// 移除首个匹配的后缀，无匹配时原样返回
    /// </summary>
    [Fact]
    public void RemovePostFix_RemovesFirstMatchingSuffix()
    {
        Assert.Equal("Hello", "Hello.txt".RemovePostFix(".txt"));
        Assert.Equal("Hello", "Hello.txt".RemovePostFix(".md", ".txt"));
        Assert.Equal("Hello", "Hello".RemovePostFix(".txt"));
        Assert.Equal(string.Empty, string.Empty.RemovePostFix(".txt"));
        Assert.Equal("Hello", "Hello".RemovePostFix());
    }

    /// <summary>
    /// 后缀比较可以忽略大小写
    /// </summary>
    [Fact]
    public void RemovePostFix_WithComparison_RespectsCaseOption()
    {
        Assert.Equal("Hello.TXT", "Hello.TXT".RemovePostFix(".txt"));
        Assert.Equal("Hello", "Hello.TXT".RemovePostFix(StringComparison.OrdinalIgnoreCase, ".txt"));
    }

    /// <summary>
    /// 移除首个匹配的前缀，无匹配时原样返回
    /// </summary>
    [Fact]
    public void RemovePreFix_RemovesFirstMatchingPrefix()
    {
        Assert.Equal("value", "prefix_value".RemovePreFix("prefix_"));
        Assert.Equal("prefix_value", "prefix_value".RemovePreFix("other_"));
        Assert.Equal("value", "PREFIX_value".RemovePreFix(StringComparison.OrdinalIgnoreCase, "prefix_"));
        Assert.Equal(string.Empty, string.Empty.RemovePreFix("x"));
    }

    /// <summary>
    /// 只替换第一次出现，未命中时原样返回
    /// </summary>
    [Fact]
    public void ReplaceFirst_ReplacesOnlyFirstOccurrence()
    {
        Assert.Equal("hi hello", "hello hello".ReplaceFirst("hello", "hi"));
        Assert.Equal("abc", "abc".ReplaceFirst("x", "y"));
        Assert.Equal("Xbc", "abc".ReplaceFirst("A", "X", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<ArgumentNullException>(() => StringExtensions.ReplaceFirst(null!, "a", "b"));
    }

    /// <summary>
    /// 按字符串分隔符切分
    /// </summary>
    [Fact]
    public void Split_SplitsByStringSeparator()
    {
        Assert.Equal(new[] { "a", "b" }, StringExtensions.Split("a::b", "::"));
        Assert.Equal(new[] { "a", "", "b" }, StringExtensions.Split("a,,b", ","));
        Assert.Equal(new[] { "a", "b" }, StringExtensions.Split("a,,b", ",", StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// 按当前平台换行符切分为行
    /// </summary>
    [Fact]
    public void SplitToLines_SplitsByEnvironmentNewLine()
    {
        var input = string.Join(Environment.NewLine, "a", "b", string.Empty);

        Assert.Equal(new[] { "a", "b", string.Empty }, input.SplitToLines());
        Assert.Equal(new[] { "a", "b" }, input.SplitToLines(StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// 帕斯卡转小驼峰
    /// </summary>
    [Fact]
    public void ToCamelCase_LowersFirstCharacter()
    {
        Assert.Equal("thisIsSample", "ThisIsSample".ToCamelCase());
        Assert.Equal("a", "A".ToCamelCase());
        Assert.Equal("xYZ", "XYZ".ToCamelCase());
        Assert.Equal(string.Empty, string.Empty.ToCamelCase());
        Assert.Equal("  ", "  ".ToCamelCase());
    }

    /// <summary>
    /// 开启缩写处理后全大写串整体转小写
    /// </summary>
    [Fact]
    public void ToCamelCase_WithAbbreviationHandling_LowersWholeWord()
    {
        Assert.Equal("xyz", "XYZ".ToCamelCase(false, true));
        Assert.Equal("thisIsSample", "ThisIsSample".ToCamelCase(false, true));
    }

    /// <summary>
    /// 小驼峰转帕斯卡
    /// </summary>
    [Fact]
    public void ToPascalCase_UppersFirstCharacter()
    {
        Assert.Equal("ThisIsSample", "thisIsSample".ToPascalCase());
        Assert.Equal("A", "a".ToPascalCase());
        Assert.Equal(string.Empty, string.Empty.ToPascalCase());
    }

    /// <summary>
    /// 转蛇形命名走 System.Text.Json 的命名策略
    /// </summary>
    [Fact]
    public void ToSnakeCase_ProducesLowerSnakeCase()
    {
        Assert.Equal("this_is_sample", "ThisIsSample".ToSnakeCase());
        Assert.Equal("this_is_sample", "thisIsSample".ToSnakeCase());
        Assert.Equal(string.Empty, string.Empty.ToSnakeCase());
    }

    /// <summary>
    /// 字符串转枚举，支持忽略大小写，非法名称抛参数异常
    /// </summary>
    [Fact]
    public void ToEnum_ParsesNameWithOptionalCaseInsensitivity()
    {
        Assert.Equal(DayOfWeek.Monday, "Monday".ToEnum<DayOfWeek>());
        Assert.Equal(DayOfWeek.Monday, "monday".ToEnum<DayOfWeek>(true));
        Assert.Throws<ArgumentException>(() => "NotADay".ToEnum<DayOfWeek>());
    }

    /// <summary>
    /// MD5 结果为大写十六进制串
    /// </summary>
    [Fact]
    public void ToMd5_ReturnsUpperCaseHex()
    {
        Assert.Equal("900150983CD24FB0D6963F7D28E17F72", "abc".ToMd5());
        Assert.Equal(32, string.Empty.ToMd5().Length);
    }

    /// <summary>
    /// 超长时截断并追加提示，未超长时原样返回
    /// </summary>
    [Fact]
    public void Truncate_AppendsMarkerOnlyWhenTooLong()
    {
        Assert.Equal("abc... (truncated)", "abcdef".Truncate(3));
        Assert.Equal("abc", "abc".Truncate(3));
        Assert.Null(StringExtensions.Truncate(null, 3));
    }

    /// <summary>
    /// 从结尾截断时在前面加提示
    /// </summary>
    [Fact]
    public void TruncateFromBeginning_PrependsMarkerOnlyWhenTooLong()
    {
        Assert.Equal("(truncated) ...def", "abcdef".TruncateFromBeginning(3));
        Assert.Equal("abc", "abc".TruncateFromBeginning(3));
        Assert.Null(StringExtensions.TruncateFromBeginning(null, 3));
    }

    /// <summary>
    /// 带后缀截断时保证结果不超过最大长度
    /// </summary>
    [Fact]
    public void TruncateWithPostfix_NeverExceedsMaxLength()
    {
        Assert.Equal("ab...", "abcdef".TruncateWithPostfix(5));
        Assert.Equal("..", "abcdef".TruncateWithPostfix(2));
        Assert.Equal(string.Empty, "abc".TruncateWithPostfix(0));
        Assert.Equal("abc", "abc".TruncateWithPostfix(5));
        Assert.Equal(string.Empty, string.Empty.TruncateWithPostfix(5));
        Assert.Null(StringExtensions.TruncateWithPostfix(null, 5));
    }

    /// <summary>
    /// 默认按 UTF8 取字节，也可指定编码
    /// </summary>
    [Fact]
    public void GetBytes_UsesUtf8ByDefault()
    {
        Assert.Equal(Encoding.UTF8.GetBytes("曦寒"), "曦寒".GetBytes());
        Assert.Equal(Encoding.ASCII.GetBytes("abc"), "abc".GetBytes(Encoding.ASCII));
        Assert.Throws<ArgumentNullException>(() => StringExtensions.GetBytes(null!));
        Assert.Throws<ArgumentNullException>(() => "abc".GetBytes(null!));
    }

    /// <summary>
    /// JSON 合法性检测
    /// </summary>
    [Theory]
    [InlineData("{}", true)]
    [InlineData("[]", true)]
    [InlineData("{\"a\":1}", true)]
    [InlineData("{", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidJson_DetectsWellFormedJson(string value, bool expected)
    {
        Assert.Equal(expected, value.IsValidJson());
    }

    /// <summary>
    /// 忽略大小写比较，双方为 null 视为相等
    /// </summary>
    [Fact]
    public void EqualsIgnoreCase_ComparesCaseInsensitively()
    {
        Assert.True("ABC".EqualsIgnoreCase("abc"));
        Assert.True(StringExtensions.EqualsIgnoreCase(null, null));
        Assert.False("abc".EqualsIgnoreCase(null));
        Assert.False("abc".EqualsIgnoreCase("abd"));
    }

    /// <summary>
    /// 首字母大小写切换，已符合时原样返回
    /// </summary>
    [Fact]
    public void FirstCharToUpperAndLower_SwitchLeadingCharacter()
    {
        Assert.Equal("Abc", "abc".FirstCharToUpper());
        Assert.Equal("Abc", "Abc".FirstCharToUpper());
        Assert.Equal(string.Empty, string.Empty.FirstCharToUpper());

        Assert.Equal("aBC", "ABC".FirstCharToLower());
        Assert.Equal("abc", "abc".FirstCharToLower());
        Assert.Equal(string.Empty, string.Empty.FirstCharToLower());
    }

    /// <summary>
    /// 包含任意一个与包含全部的判定
    /// </summary>
    [Fact]
    public void ContainsAnyAndContainsAll_CheckSubstringSets()
    {
        var source = "hello world";

        Assert.True(source.ContainsAny(new[] { "zzz", "world" }));
        Assert.False(source.ContainsAny(new[] { "zzz" }));
        Assert.True(source.ContainsAll(new[] { "hello", "world" }));
        Assert.False(source.ContainsAll(new[] { "hello", "zzz" }));
        Assert.True(source.ContainsAll(Array.Empty<string>()));
        Assert.False(source.ContainsAny(Array.Empty<string>()));
        Assert.True(source.ContainsAny(new[] { "WORLD" }, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 集合为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void ContainsAnyAndContainsAll_WhenValuesIsNull_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => "abc".ContainsAny(null!));
        Assert.Throws<ArgumentNullException>(() => "abc".ContainsAll(null!));
    }

    /// <summary>
    /// 定长切分，末段允许不足长度
    /// </summary>
    [Fact]
    public void SplitInParts_SplitsIntoFixedSizeChunks()
    {
        Assert.Equal(new[] { "abc", "def", "g" }, "abcdefg".SplitInParts(3));
        Assert.Equal(new[] { "abc" }, "abc".SplitInParts(5));
        Assert.Empty(string.Empty.SplitInParts(3));
    }

    /// <summary>
    /// 长度非法或源串为 null 时抛异常（迭代器方法在枚举时抛出）
    /// </summary>
    [Fact]
    public void SplitInParts_WhenArgumentInvalid_Throws()
    {
        Assert.Throws<ArgumentException>(() => "abc".SplitInParts(0).ToList());
        Assert.Throws<ArgumentNullException>(() => StringExtensions.SplitInParts(null!, 3).ToList());
    }

    /// <summary>
    /// 字符包含判断
    /// </summary>
    [Fact]
    public void Contains_WithChar_DetectsPresence()
    {
        Assert.True(StringExtensions.Contains("abc", 'b'));
        Assert.False(StringExtensions.Contains("abc", 'z'));
        Assert.False(StringExtensions.Contains(string.Empty, 'a'));
    }

    /// <summary>
    /// 格式化等价于 string.Format
    /// </summary>
    [Fact]
    public void Format_DelegatesToStringFormat()
    {
        Assert.Equal("1-a", "{0}-{1}".Format(1, "a"));
        Assert.Equal("no args", "no args".Format());
    }

    /// <summary>
    /// 空白时回落默认值
    /// </summary>
    [Fact]
    public void DefaultIfNullOrWhiteSpace_FallsBackOnBlank()
    {
        Assert.Equal("默认", StringExtensions.DefaultIfNullOrWhiteSpace(null, "默认"));
        Assert.Equal("默认", "   ".DefaultIfNullOrWhiteSpace("默认"));
        Assert.Equal("x", "x".DefaultIfNullOrWhiteSpace("默认"));
        Assert.Null("   ".DefaultIfNullOrWhiteSpace(null));
    }

    /// <summary>
    /// 仅在非空白时执行回调
    /// </summary>
    [Fact]
    public void IfNotNullOrWhiteSpace_RunsActionOnlyForRealContent()
    {
        List<string> collected = [];

        "value".IfNotNullOrWhiteSpace(collected.Add);
        "   ".IfNotNullOrWhiteSpace(collected.Add);
        StringExtensions.IfNotNullOrWhiteSpace(null, collected.Add);

        Assert.Equal(new[] { "value" }, collected);
    }

    /// <summary>
    /// 字节大小按编码计算
    /// </summary>
    [Fact]
    public void GetByteSize_CountsByEncoding()
    {
        Assert.Equal(6, "曦寒".GetByteSize());
        Assert.Equal(3, "abc".GetByteSize(Encoding.ASCII));
        Assert.Equal(0, string.Empty.GetByteSize());
        Assert.Throws<ArgumentNullException>(() => StringExtensions.GetByteSize(null!));
    }

    /// <summary>
    /// 移除所有空白字符，无空白时返回原实例
    /// </summary>
    [Fact]
    public void RemoveWhiteSpaces_StripsEveryWhitespace()
    {
        var untouched = "abc";

        Assert.Equal("abc", " a b\tc ".RemoveWhiteSpaces());
        Assert.Same(untouched, untouched.RemoveWhiteSpaces());
        Assert.Equal(string.Empty, "   ".RemoveWhiteSpaces());
        Assert.Throws<ArgumentNullException>(() => StringExtensions.RemoveWhiteSpaces(null!));
    }

    /// <summary>
    /// 移除控制字符与空白字符
    /// </summary>
    [Fact]
    public void RemoveInvisibleChars_StripsControlAndWhitespace()
    {
        var input = "a" + (char)1 + " b";

        Assert.Equal("ab", input.RemoveInvisibleChars());
        Assert.Equal("abc", "abc".RemoveInvisibleChars());
    }

    /// <summary>
    /// 安全截取自动收敛越界的起点与长度
    /// </summary>
    [Fact]
    public void SafeSubstring_ClampsIndexAndLength()
    {
        var source = "abcdef";

        Assert.Equal("cdef", source.SafeSubstring(2));
        Assert.Equal("cd", source.SafeSubstring(2, 2));
        Assert.Equal(string.Empty, source.SafeSubstring(10));
        Assert.Equal("abc", source.SafeSubstring(-1, 3));
        Assert.Equal("ef", source.SafeSubstring(4, 10));
        Assert.Equal(string.Empty, source.SafeSubstring(1, -5));
        Assert.Throws<ArgumentNullException>(() => StringExtensions.SafeSubstring(null!, 0));
    }

    /// <summary>
    /// 编辑距离按经典 Levenshtein 定义
    /// </summary>
    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("abc", "abc", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("", "", 0)]
    public void LevenshteinDistance_MatchesClassicDefinition(string left, string right, int expected)
    {
        Assert.Equal(expected, left.LevenshteinDistance(right));
    }

    /// <summary>
    /// 中文字符检测
    /// </summary>
    [Fact]
    public void ContainsChinese_DetectsCjkCharacters()
    {
        Assert.True("曦寒".ContainsChinese());
        Assert.True("abc曦".ContainsChinese());
        Assert.False("abc".ContainsChinese());
        Assert.False(string.Empty.ContainsChinese());
    }

    /// <summary>
    /// 反转字符顺序，长度不超过一时原样返回
    /// </summary>
    [Fact]
    public void Reverse_ReversesCharacters()
    {
        Assert.Equal("cba", "abc".Reverse());
        Assert.Equal("a", "a".Reverse());
        Assert.Equal(string.Empty, string.Empty.Reverse());
        Assert.Throws<ArgumentNullException>(() => StringExtensions.Reverse(null!));
    }

    /// <summary>
    /// 补齐到指定长度，默认右侧补齐
    /// </summary>
    [Fact]
    public void PadToLength_PadsOnRequestedSide()
    {
        Assert.Equal("ab   ", "ab".PadToLength(5));
        Assert.Equal("000ab", "ab".PadToLength(5, '0', true));
        Assert.Equal("ab000", "ab".PadToLength(5, '0'));
        Assert.Equal("abcdef", "abcdef".PadToLength(3));
    }

    /// <summary>
    /// 重复拼接，次数不大于 0 时返回空串
    /// </summary>
    [Fact]
    public void Repeat_ConcatenatesGivenTimes()
    {
        Assert.Equal("ababab", "ab".Repeat(3));
        Assert.Equal("ab", "ab".Repeat(1));
        Assert.Equal(string.Empty, "ab".Repeat(0));
        Assert.Equal(string.Empty, "ab".Repeat(-1));
        Assert.Equal(string.Empty, string.Empty.Repeat(5));
        Assert.Throws<ArgumentNullException>(() => StringExtensions.Repeat(null!, 2));
    }
}

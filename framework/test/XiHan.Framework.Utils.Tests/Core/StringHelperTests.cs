// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.Utils.Tests.Core;

/// <summary>
/// 字符串帮助类测试
/// </summary>
/// <remarks>
/// 锁三处缺陷：拼接时用值比较判断末项导致重复元素丢分隔符；
/// 整体替换在被替换子串为空时死循环；
/// 按显示宽度截断时总是多带一个字符出来、并且会把代理对拆开。
/// </remarks>
public class StringHelperTests
{
    #region 拼接

    /// <summary>
    /// 每个元素之间都有分隔符，重复元素不影响
    /// </summary>
    /// <remarks>
    /// 原实现用 <c>item == enumerable.LastOrDefault()</c> 判断"是不是最后一项"，这是按值比较：
    /// 只要集合里有元素与末项相等，那个位置的分隔符就被吞掉，["a","b","a"] 会拼成 "ab,a"。
    /// </remarks>
    [Theory]
    [InlineData(new[] { "a", "b", "a" }, "a,b,a")]
    [InlineData(new[] { "x", "x", "x" }, "x,x,x")]
    [InlineData(new[] { "a", "b", "c" }, "a,b,c")]
    [InlineData(new[] { "only" }, "only")]
    [InlineData(new string[0], "")]
    public void GetEnumerableStr_JoinsEveryElementWithSeparator(string[] source, string expected)
    {
        Assert.Equal(expected, StringHelper.GetEnumerableStr(source));
    }

    /// <summary>
    /// 自定义分隔符同样逐项分隔
    /// </summary>
    [Fact]
    public void GetEnumerableStr_WithCustomSeparator_JoinsEveryElement()
    {
        Assert.Equal("a|b|a", StringHelper.GetEnumerableStr(["a", "b", "a"], '|'));
    }

    /// <summary>
    /// 不允许重复时先去重再拼接
    /// </summary>
    [Fact]
    public void GetEnumerableStr_WhenDuplicatesDisallowed_DistinctsFirst()
    {
        Assert.Equal("a,b", StringHelper.GetEnumerableStr(["a", "b", "a"], ',', false));
    }

    /// <summary>
    /// 列表与数组重载与序列重载结果一致
    /// </summary>
    [Fact]
    public void GetListStr_AndGetArrayStr_MatchEnumerableOverload()
    {
        string[] source = ["a", "b", "a"];

        Assert.Equal("a,b,a", StringHelper.GetListStr(source));
        Assert.Equal("a,b,a", StringHelper.GetArrayStr(source));
    }

    /// <summary>
    /// 源序列为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetEnumerableStr_WhenSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StringHelper.GetEnumerableStr(null!));
    }

    #endregion 拼接

    #region 整体替换

    /// <summary>
    /// 所有出现位置都被替换
    /// </summary>
    [Theory]
    [InlineData("a-b-a", "-", "+", "a+b+a")]
    [InlineData("aaa", "a", "b", "bbb")]
    [InlineData("abc", "x", "y", "abc")]
    [InlineData("aa", "aa", "b", "b")]
    public void FormatReplaceStr_ReplacesEveryOccurrence(string content, string oldStr, string newStr, string expected)
    {
        Assert.Equal(expected, StringHelper.FormatReplaceStr(content, oldStr, newStr));
    }

    /// <summary>
    /// 被替换子串为空时抛参数异常，而不是死循环
    /// </summary>
    /// <remarks>
    /// 原实现自己手写查找拼接，空串时 <c>IndexOf("")</c> 恒返回当前位置、游标又加 0，
    /// 循环永不前进并持续向缓冲区追加，调用即挂死直到 OOM。
    /// </remarks>
    [Fact]
    public async Task FormatReplaceStr_WhenOldStringIsEmpty_ThrowsInsteadOfHanging()
    {
        var call = Task.Run(() => StringHelper.FormatReplaceStr("abc", string.Empty, "x"));

        // 超时即判定为死循环，不让用例挂死
        var completed = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(call, completed);
        await Assert.ThrowsAsync<ArgumentException>(() => call);
    }

    /// <summary>
    /// 入参为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void FormatReplaceStr_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StringHelper.FormatReplaceStr(null!, "a", "b"));
        Assert.Throws<ArgumentNullException>(() => StringHelper.FormatReplaceStr("abc", null!, "b"));
    }

    #endregion 整体替换

    #region 显示宽度

    /// <summary>
    /// 显示宽度按 ASCII 记 1、其余记 2 计算
    /// </summary>
    /// <remarks>
    /// 原实现靠"转 ASCII 后哪些字节是 63"识别非 ASCII 字符，
    /// 输入里本来就有的问号同样是 63，会被误记成 2。
    /// </remarks>
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("?", 1)]
    [InlineData("a?b", 3)]
    [InlineData("中", 2)]
    [InlineData("a中?", 4)]
    [InlineData("😀", 2)]
    public void GetStrLength_CountsAsciiAsOneAndOthersAsTwo(string input, int expected)
    {
        Assert.Equal(expected, StringHelper.GetStrLength(input));
    }

    #endregion 显示宽度

    #region 截断

    /// <summary>
    /// 截断结果的显示宽度不超过指定长度
    /// </summary>
    /// <remarks>
    /// 原实现先追加字符再判断是否超长，总是多带一个字符出来，
    /// <c>ClipString("abcdefgh", 4)</c> 会返回 5 个字符的 "abcde"。
    /// </remarks>
    [Theory]
    [InlineData("abcdefgh", 4, "abcd")]
    [InlineData("abcdefgh", 8, "abcdefgh")]
    [InlineData("abc", 10, "abc")]
    [InlineData("中文测试", 4, "中文")]
    [InlineData("ab中", 4, "ab中")]
    [InlineData("ab中", 2, "ab")]
    public void ClipString_NeverExceedsRequestedWidth(string input, int len, string expected)
    {
        var clipped = StringHelper.ClipString(input, len);

        Assert.Equal(expected, clipped);
        Assert.True(StringHelper.GetStrLength(clipped) <= len);
    }

    /// <summary>
    /// 奇数长度表示截断时追加省略号，可用宽度相应减一
    /// </summary>
    [Theory]
    [InlineData("abcdefgh", 5, "abcd…")]
    [InlineData("ab中", 3, "ab…")]
    [InlineData("abc", 9, "abc")]
    public void ClipString_WithOddLength_AppendsEllipsisOnlyWhenTruncated(string input, int len, string expected)
    {
        Assert.Equal(expected, StringHelper.ClipString(input, len));
    }

    /// <summary>
    /// 代理对不会被从中间截断
    /// </summary>
    /// <remarks>
    /// 原实现用 ASCII 字节下标去索引原字符串，遇到代理对时字节数与字符数不再一一对应，
    /// 越界后被一个空的 catch 静默吞掉，结果可能带出半个字符。
    /// </remarks>
    [Theory]
    [InlineData(6, "a😀b😀")]
    [InlineData(4, "a😀b")]
    [InlineData(2, "a")]
    public void ClipString_KeepsSurrogatePairsIntact(int len, string expected)
    {
        var clipped = StringHelper.ClipString("a😀b😀c", len);

        Assert.Equal(expected, clipped);

        // 代理对被拆散时，落单的那一半会被枚举成替换字符 U+FFFD
        Assert.All(clipped.EnumerateRunes(), rune => Assert.NotEqual(Rune.ReplacementChar, rune));
    }

    /// <summary>
    /// 非正长度返回空串
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ClipString_WhenLengthNotPositive_ReturnsEmpty(int len)
    {
        Assert.Equal(string.Empty, StringHelper.ClipString("abc", len));
    }

    /// <summary>
    /// 入参为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void ClipString_WhenInputIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StringHelper.ClipString(null!, 4));
        Assert.Throws<ArgumentNullException>(() => StringHelper.GetStrLength(null!));
    }

    #endregion 截断

    #region 数字主键校验

    /// <summary>
    /// 不含前导零的非零正整数才是合法主键
    /// </summary>
    /// <remarks>
    /// 原模式 <c>^[1-9]*[0-9]*$</c> 的两段都能匹配空，实际等价于"全是数字"：
    /// 文档声明的"0 除外"落空，前导零也照过，而 <c>$</c> 还放行串尾的换行。
    /// </remarks>
    [Theory]
    [InlineData("1", true)]
    [InlineData("123", true)]
    [InlineData("9007199254740993", true)]
    [InlineData("0", false)]
    [InlineData("0123", false)]
    [InlineData("00", false)]
    [InlineData("12\n", false)]
    [InlineData("\n12", false)]
    [InlineData("1 2", false)]
    [InlineData("-1", false)]
    [InlineData("1.0", false)]
    [InlineData("a", false)]
    [InlineData("", false)]
    public void IsNumberId_AcceptsOnlyNonZeroIntegersWithoutLeadingZero(string value, bool expected)
    {
        Assert.Equal(expected, StringHelper.IsNumberId(value));
    }

    /// <summary>
    /// 入参为 null 时返回假
    /// </summary>
    [Fact]
    public void IsNumberId_WhenValueIsNull_ReturnsFalse()
    {
        Assert.False(StringHelper.IsNumberId(null));
    }

    #endregion 数字主键校验
}

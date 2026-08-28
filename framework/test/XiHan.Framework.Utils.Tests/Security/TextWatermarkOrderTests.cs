// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 文本水印嵌入顺序回归测试
/// </summary>
/// <remarks>
/// <para>
/// 修复前 <see cref="TextWatermarkHelper.EmbedWatermark"/> 有两处坐标错乱：句尾插入不补偿已插入的字符数；
/// 剩余位按 <c>(i - index + 1) * text.Length / (...)</c> 算出的位置普遍小于前面的句尾位置，
/// 于是后嵌入的位在最终串里反而排在前面。<c>ExtractWatermark</c> 按出现顺序还原，拿到的是乱序位串。
/// </para>
/// <para>
/// 这里锁的是「水印字符在结果串里的出现顺序必须与位串下标顺序一致」这条不变量：
/// 期望序列由 <c>FromStringToBinary</c> 的实际产出现算，因此不依赖具体采用哪套定长编码，
/// 只要编码口径变了也不会误报。
/// </para>
/// </remarks>
public class TextWatermarkOrderTests
{
    /// <summary>
    /// 承载水印的八个不可见字符（与被测实现内部的私有数组一致）
    /// </summary>
    private static readonly char[] InvisibleChars =
    [
        '\u200B', // 零宽空格
        '\u200C', // 零宽不连字
        '\u200D', // 零宽连字
        '\u2060', // 单词连接符
        '\u2061', // 函数应用
        '\u2062', // 不可见乘号
        '\u2063', // 不可见分隔符
        '\u2064'  // 不可见加号
    ];

    /// <summary>
    /// 水印字符的出现顺序必须与位串顺序严格一致
    /// </summary>
    /// <remarks>
    /// 四个用例分别覆盖：多句子且句尾抵达文末、末尾句为纯空白（剩余位需要在尾部区间铺开）、
    /// 中文句读切分、以及完全没有句末标点的单句文本。
    /// </remarks>
    [Theory]
    [InlineData("Hello world. This is a watermark test. Third sentence here.", "XIHAN")]
    [InlineData("One. Two.   ", "XIHAN")]
    [InlineData("单句中文文本。第二句。第三句。", "水印")]
    [InlineData("No sentence terminator here", "X")]
    public void EmbedWatermark_KeepsWatermarkCharactersInBitOrder(string text, string watermark)
    {
        var watermarked = TextWatermarkHelper.EmbedWatermark(text, watermark);

        Assert.Equal(ExpectedSequence(watermark), ExtractSequence(watermarked));
    }

    /// <summary>
    /// 水印位数远多于句子数时，剩余位同样按顺序铺开
    /// </summary>
    /// <remarks>
    /// 这是修复前最容易乱序的路径：句子只有两句，其余几十位全部走「补位」分支。
    /// </remarks>
    [Fact]
    public void EmbedWatermark_WhenBitsFarExceedSentences_StillKeepsBitOrder()
    {
        const string Text = "One. Two.   ";
        const string Watermark = "LONGER-WATERMARK-VALUE";

        var watermarked = TextWatermarkHelper.EmbedWatermark(Text, Watermark);

        Assert.Equal(ExpectedSequence(Watermark), ExtractSequence(watermarked));
    }

    /// <summary>
    /// 每个水印位都被写入且只写入一次，数量与位串长度相等
    /// </summary>
    [Fact]
    public void EmbedWatermark_EmbedsEveryBitExactlyOnce()
    {
        const string Text = "Hello world. This is a watermark test. Third sentence here.";
        const string Watermark = "XIHAN";

        var watermarked = TextWatermarkHelper.EmbedWatermark(Text, Watermark);

        Assert.Equal(Watermark.FromStringToBinary().Length, ExtractSequence(watermarked).Length);
    }

    /// <summary>
    /// 顺序修复不能以改动可见正文为代价
    /// </summary>
    [Theory]
    [InlineData("Hello world. This is a watermark test. Third sentence here.", "XIHAN")]
    [InlineData("One. Two.   ", "XIHAN")]
    [InlineData("单句中文文本。第二句。第三句。", "水印")]
    [InlineData("No sentence terminator here", "X")]
    public void EmbedWatermark_LeavesVisibleTextByteForByteIntact(string text, string watermark)
    {
        var watermarked = TextWatermarkHelper.EmbedWatermark(text, watermark);

        var visible = new string([.. watermarked.Where(c => Array.IndexOf(InvisibleChars, c) < 0)]);
        Assert.Equal(text, visible);
    }

    /// <summary>
    /// 重复出现的句子不会都定位到首次出现处，水印仍然按顺序铺开
    /// </summary>
    [Fact]
    public void EmbedWatermark_WithRepeatedSentences_KeepsBitOrder()
    {
        const string Text = "Same. Same. Same. Same.";
        const string Watermark = "AB";

        var watermarked = TextWatermarkHelper.EmbedWatermark(Text, Watermark);

        Assert.Equal(ExpectedSequence(Watermark), ExtractSequence(watermarked));
    }

    /// <summary>
    /// 按位串顺序算出的水印字符序列
    /// </summary>
    private static string ExpectedSequence(string watermark)
    {
        var bits = watermark.FromStringToBinary();
        return new string([.. bits.Select(bit => InvisibleChars[bit % InvisibleChars.Length])]);
    }

    /// <summary>
    /// 按出现顺序取出文本里的水印字符序列（与 ExtractWatermark 的扫描口径一致）
    /// </summary>
    private static string ExtractSequence(string watermarked)
    {
        return new string([.. watermarked.Where(c => Array.IndexOf(InvisibleChars, c) >= 0)]);
    }
}

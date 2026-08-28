// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 文本零宽水印辅助类测试
/// </summary>
/// <remarks>
/// 水印靠八个 Unicode 零宽/不可见字符承载，最核心的可见契约是
/// 「加水印后可见文本一字不变」——把这八个字符剔除必须还原成原文，
/// 否则水印就不是隐形的，会污染业务内容。
/// </remarks>
public class TextWatermarkHelperTests
{
    /// <summary>
    /// 承载水印的八个不可见字符
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

    private const string PlainText = "Hello world. This is a watermark test. Third sentence here.";

    /// <summary>
    /// 加水印后可见内容不变
    /// </summary>
    [Fact]
    public void EmbedWatermark_KeepsVisibleTextIntact()
    {
        var watermarked = TextWatermarkHelper.EmbedWatermark(PlainText, "XIHAN");

        Assert.NotEqual(PlainText, watermarked);
        Assert.Equal(PlainText, StripInvisibleChars(watermarked));
    }

    /// <summary>
    /// 加水印后能被检出，原文检不出
    /// </summary>
    [Fact]
    public void ContainsWatermark_DetectsOnlyWatermarkedText()
    {
        Assert.False(TextWatermarkHelper.ContainsWatermark(PlainText));
        Assert.True(TextWatermarkHelper.ContainsWatermark(TextWatermarkHelper.EmbedWatermark(PlainText, "XIHAN")));
    }

    /// <summary>
    /// 空文本或空水印时原样返回，不做任何插入
    /// </summary>
    [Theory]
    [InlineData("", "XIHAN")]
    [InlineData("Hello world.", "")]
    [InlineData("", "")]
    public void EmbedWatermark_WithBlankInput_ReturnsOriginalText(string text, string watermark)
    {
        Assert.Equal(text, TextWatermarkHelper.EmbedWatermark(text, watermark));
    }

    /// <summary>
    /// 空文本与无水印文本的检出结果都是否
    /// </summary>
    [Fact]
    public void ContainsWatermark_WithBlankText_ReturnsFalse()
    {
        Assert.False(TextWatermarkHelper.ContainsWatermark(string.Empty));
    }

    /// <summary>
    /// 从没有水印的文本里提取得到空串
    /// </summary>
    [Fact]
    public void ExtractWatermark_WithoutWatermark_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextWatermarkHelper.ExtractWatermark(PlainText));
        Assert.Equal(string.Empty, TextWatermarkHelper.ExtractWatermark(string.Empty));
    }

    /// <summary>
    /// 带密钥加水印同样不改动可见内容
    /// </summary>
    [Fact]
    public void EmbedWatermark_WithKey_KeepsVisibleTextIntact()
    {
        var watermarked = TextWatermarkHelper.EmbedWatermark(PlainText, "XIHAN", "watermark-key");

        Assert.True(TextWatermarkHelper.ContainsWatermark(watermarked));
        Assert.Equal(PlainText, StripInvisibleChars(watermarked));
    }

    /// <summary>
    /// 中文正文加水印后可见内容不变
    /// </summary>
    [Fact]
    public void EmbedWatermark_WithChineseText_KeepsVisibleTextIntact()
    {
        const string ChineseText = "曦寒框架很好用。这是第二句话。第三句在这里。";

        var watermarked = TextWatermarkHelper.EmbedWatermark(ChineseText, "XIHAN");

        Assert.True(TextWatermarkHelper.ContainsWatermark(watermarked));
        Assert.Equal(ChineseText, StripInvisibleChars(watermarked));
    }

    /// <summary>
    /// 嵌入水印后提取应当还原出原始水印串
    /// </summary>
    /// <remarks>
    /// 往返链路原先有两处对不上，均已按产品缺陷修复，这里锁住修复结果：
    /// 一是 <c>EmbedWatermark</c> 的剩余位插入坐标不补偿已插入字符数，提取顺序与嵌入顺序不一致；
    /// 二是它依赖的 <c>FromStringToBinary</c> 用 <c>Convert.ToString(b, 8)</c> 生成变长八进制串，
    /// 而 <c>FromBinaryToString</c> 按固定 3 字符切片并用 <c>byte.TryParse</c> 当十进制解析，
    /// 编码与解码口径完全不同（"XIHAN" 会读回成替换字符加 "onet"）。
    /// 现两侧统一为「每字节定长 3 位八进制」，往返可逆。
    /// </remarks>
    [Fact]
    public void EmbedWatermarkExtractWatermark_RoundTrips()
    {
        var watermarked = TextWatermarkHelper.EmbedWatermark(PlainText, "XIHAN");

        Assert.Equal("XIHAN", TextWatermarkHelper.ExtractWatermark(watermarked));
    }

    /// <summary>
    /// HTML 文本加水印后可见内容不变
    /// </summary>
    [Fact]
    public void EmbedHtmlWatermark_KeepsMarkupIntact()
    {
        const string Html = "<div><p>Hello</p><p>World</p><span>Third</span></div>";

        var watermarked = TextWatermarkHelper.EmbedHtmlWatermark(Html, "XIHAN");

        Assert.True(TextWatermarkHelper.ContainsWatermark(watermarked));
        Assert.Equal(Html, StripInvisibleChars(watermarked));
    }

    /// <summary>
    /// 不含标签的文本回落到普通文本水印
    /// </summary>
    [Fact]
    public void EmbedHtmlWatermark_WithoutTags_FallsBackToPlainTextWatermark()
    {
        var watermarked = TextWatermarkHelper.EmbedHtmlWatermark(PlainText, "XIHAN");

        Assert.True(TextWatermarkHelper.ContainsWatermark(watermarked));
        Assert.Equal(PlainText, StripInvisibleChars(watermarked));
    }

    /// <summary>
    /// HTML 水印提取复用普通提取逻辑
    /// </summary>
    [Fact]
    public void ExtractHtmlWatermark_DelegatesToExtractWatermark()
    {
        const string Html = "<div><p>Hello</p><p>World</p></div>";

        var watermarked = TextWatermarkHelper.EmbedHtmlWatermark(Html, "XIHAN");

        Assert.Equal(
            TextWatermarkHelper.ExtractWatermark(watermarked),
            TextWatermarkHelper.ExtractHtmlWatermark(watermarked));
    }

    /// <summary>
    /// 空 HTML 或空水印时原样返回
    /// </summary>
    [Theory]
    [InlineData("", "XIHAN")]
    [InlineData("<p>Hello</p>", "")]
    public void EmbedHtmlWatermark_WithBlankInput_ReturnsOriginalText(string html, string watermark)
    {
        Assert.Equal(html, TextWatermarkHelper.EmbedHtmlWatermark(html, watermark));
    }

    /// <summary>
    /// 批量水印为每个文本块加上带连续编号的水印，可见内容不变
    /// </summary>
    [Fact]
    public void EmbedBatchWatermark_WatermarksEveryBlock()
    {
        var blocks = new List<string> { "First block. Second sentence.", "Another block here. And more." };

        var watermarked = TextWatermarkHelper.EmbedBatchWatermark(blocks, "DOC");

        Assert.Equal(blocks.Count, watermarked.Count);
        for (var i = 0; i < blocks.Count; i++)
        {
            Assert.True(TextWatermarkHelper.ContainsWatermark(watermarked[i]));
            Assert.Equal(blocks[i], StripInvisibleChars(watermarked[i]));
        }
    }

    /// <summary>
    /// 批量水印标识符为空时原样返回集合
    /// </summary>
    [Fact]
    public void EmbedBatchWatermark_WithBlankIdentifier_ReturnsInputUnchanged()
    {
        var blocks = new List<string> { "First block.", "Second block." };

        Assert.Same(blocks, TextWatermarkHelper.EmbedBatchWatermark(blocks, string.Empty));
    }

    /// <summary>
    /// 元数据水印同样不改动可见内容
    /// </summary>
    [Fact]
    public void EmbedMetadata_KeepsVisibleTextIntact()
    {
        var metadata = new WatermarkMetadata { Owner = "XiHan", Version = 3 };

        var watermarked = TextWatermarkHelper.EmbedMetadata(PlainText, metadata);

        Assert.True(TextWatermarkHelper.ContainsWatermark(watermarked));
        Assert.Equal(PlainText, StripInvisibleChars(watermarked));
    }

    /// <summary>
    /// 元数据为 null 或文本为空时原样返回
    /// </summary>
    [Fact]
    public void EmbedMetadata_WithBlankInput_ReturnsOriginalText()
    {
        Assert.Equal(PlainText, TextWatermarkHelper.EmbedMetadata<WatermarkMetadata>(PlainText, null!));
        Assert.Equal(string.Empty, TextWatermarkHelper.EmbedMetadata(string.Empty, new WatermarkMetadata()));
    }

    /// <summary>
    /// 从没有水印的文本里提取元数据得到默认值
    /// </summary>
    [Fact]
    public void ExtractMetadata_WithoutWatermark_ReturnsDefault()
    {
        Assert.Null(TextWatermarkHelper.ExtractMetadata<WatermarkMetadata>(PlainText));
    }

    /// <summary>
    /// 剔除所有承载水印的不可见字符
    /// </summary>
    private static string StripInvisibleChars(string text)
    {
        return new string([.. text.Where(c => Array.IndexOf(InvisibleChars, c) < 0)]);
    }

    /// <summary>
    /// 测试用元数据载体
    /// </summary>
    public class WatermarkMetadata
    {
        /// <summary>
        /// 归属方
        /// </summary>
        public string Owner { get; set; } = string.Empty;

        /// <summary>
        /// 版本号
        /// </summary>
        public int Version { get; set; }
    }
}

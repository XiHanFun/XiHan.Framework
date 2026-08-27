// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Utils;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 语义化版本解析与比较测试
/// </summary>
/// <remarks>
/// 整个升级引擎的「是否需要升级」「脚本按什么顺序执行」「数据库版本推进到哪里」
/// 全部建立在这个纯函数之上，所以解析矩阵、比较矩阵、排序稳定性都要覆盖到位。
/// </remarks>
public class SemanticVersionTests
{
    /// <summary>
    /// 合法版本串按主次修订解析，缺省段补零，多余段与预发布后缀被裁掉
    /// </summary>
    /// <param name="value">版本字符串</param>
    /// <param name="major">期望主版本号</param>
    /// <param name="minor">期望次版本号</param>
    /// <param name="patch">期望修订号</param>
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("0.0.0", 0, 0, 0)]
    [InlineData("10.20.30", 10, 20, 30)]
    [InlineData("1", 1, 0, 0)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("1.2.3.4", 1, 2, 3)]
    [InlineData("  1.2.3  ", 1, 2, 3)]
    [InlineData("1.2.3-beta.1", 1, 2, 3)]
    [InlineData("1.2.3-rc1+build.7", 1, 2, 3)]
    [InlineData("2.0.0-alpha", 2, 0, 0)]
    public void TryParse_WhenValueIsValid_ReturnsTrueWithParsedParts(string value, int major, int minor, int patch)
    {
        var parsed = SemanticVersion.TryParse(value, out var version);

        Assert.True(parsed);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
    }

    /// <summary>
    /// 非法版本串一律解析失败
    /// </summary>
    /// <param name="value">版本字符串</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("v1.2.3")]
    [InlineData("1.a.3")]
    [InlineData("1.2.c")]
    [InlineData("...")]
    [InlineData("-1.2.3")]
    [InlineData("99999999999.0.0")]
    public void TryParse_WhenValueIsInvalid_ReturnsFalse(string? value)
    {
        var parsed = SemanticVersion.TryParse(value, out _);

        Assert.False(parsed);
    }

    /// <summary>
    /// 解析失败时输出参数被置为零版本，调用方拿到的是确定值而不是脏数据
    /// </summary>
    [Fact]
    public void TryParse_WhenValueIsInvalid_OutputsZeroVersion()
    {
        Assert.False(SemanticVersion.TryParse("not-a-version", out var version));

        Assert.Equal(0, version.Major);
        Assert.Equal(0, version.Minor);
        Assert.Equal(0, version.Patch);
    }

    /// <summary>
    /// 构造函数原样保留三段版本号
    /// </summary>
    [Fact]
    public void Constructor_KeepsMajorMinorPatch()
    {
        var version = new SemanticVersion(3, 14, 15);

        Assert.Equal(3, version.Major);
        Assert.Equal(14, version.Minor);
        Assert.Equal(15, version.Patch);
    }

    /// <summary>
    /// 结构体默认值等价于 0.0.0
    /// </summary>
    [Fact]
    public void Default_IsZeroVersion()
    {
        var version = default(SemanticVersion);

        Assert.Equal("0.0.0", version.ToString());
    }

    /// <summary>
    /// 字符串表示固定为「主.次.修订」，不带预发布后缀
    /// </summary>
    [Fact]
    public void ToString_FormatsAsMajorMinorPatch()
    {
        Assert.Equal("1.2.3", new SemanticVersion(1, 2, 3).ToString());
        Assert.Equal("0.0.0", new SemanticVersion(0, 0, 0).ToString());
    }

    /// <summary>
    /// 比较优先级为主版本 &gt; 次版本 &gt; 修订号，且反向比较结果取反
    /// </summary>
    /// <param name="leftMajor">左主版本</param>
    /// <param name="leftMinor">左次版本</param>
    /// <param name="leftPatch">左修订号</param>
    /// <param name="rightMajor">右主版本</param>
    /// <param name="rightMinor">右次版本</param>
    /// <param name="rightPatch">右修订号</param>
    /// <param name="expectedSign">期望比较符号</param>
    [Theory]
    [InlineData(1, 0, 0, 1, 0, 0, 0)]
    [InlineData(2, 0, 0, 1, 9, 9, 1)]
    [InlineData(1, 0, 0, 2, 0, 0, -1)]
    [InlineData(1, 2, 0, 1, 10, 0, -1)]
    [InlineData(1, 0, 10, 1, 0, 9, 1)]
    [InlineData(0, 0, 1, 0, 0, 0, 1)]
    public void CompareTo_ComparesMajorThenMinorThenPatch(
        int leftMajor, int leftMinor, int leftPatch,
        int rightMajor, int rightMinor, int rightPatch,
        int expectedSign)
    {
        var left = new SemanticVersion(leftMajor, leftMinor, leftPatch);
        var right = new SemanticVersion(rightMajor, rightMinor, rightPatch);

        Assert.Equal(expectedSign, Math.Sign(left.CompareTo(right)));
        Assert.Equal(-expectedSign, Math.Sign(right.CompareTo(left)));
    }

    /// <summary>
    /// 字符串比较按语义顺序而不是字典序
    /// </summary>
    /// <param name="left">左侧版本字符串</param>
    /// <param name="right">右侧版本字符串</param>
    /// <param name="expectedSign">期望比较符号</param>
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.1.0", -1)]
    [InlineData("1.0.10", "1.0.9", 1)]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("2.0.0", "10.0.0", -1)]
    [InlineData("1.2", "1.2.0", 0)]
    public void Compare_UsesSemanticOrderNotLexicalOrder(string left, string right, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(SemanticVersion.Compare(left, right)));
    }

    /// <summary>
    /// 无法解析的版本串在比较时按 0.0.0 处理
    /// </summary>
    /// <param name="left">左侧版本字符串</param>
    /// <param name="right">右侧版本字符串</param>
    /// <param name="expectedSign">期望比较符号</param>
    [Theory]
    [InlineData(null, "0.0.0", 0)]
    [InlineData("0.0.0", null, 0)]
    [InlineData("abc", "0.0.1", -1)]
    [InlineData("1.0.0", null, 1)]
    [InlineData("", "", 0)]
    public void Compare_WhenValueUnparsable_TreatsItAsZeroVersion(string? left, string? right, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(SemanticVersion.Compare(left, right)));
    }

    /// <summary>
    /// 预发布后缀在比较时被忽略，仅比较三段数值
    /// </summary>
    /// <remarks>
    /// 这是当前实现刻意的简化（TryParse 会在第一个 '-' 处截断），与 SemVer 规范
    /// 「1.2.3-alpha &lt; 1.2.3」的排序不同。此处锁住现有契约，规范差异已记入交付报告。
    /// </remarks>
    [Fact]
    public void Compare_WhenOnlyPreReleaseTagDiffers_TreatsVersionsAsEqual()
    {
        Assert.Equal(0, SemanticVersion.Compare("1.2.3-alpha", "1.2.3-rc.2"));
        Assert.Equal(0, SemanticVersion.Compare("1.2.3-alpha", "1.2.3"));
    }

    /// <summary>
    /// 排序结果按语义升序，10 排在 9 之后而不是之前
    /// </summary>
    [Fact]
    public void Sort_ByDefaultComparer_OrdersAscendingSemantically()
    {
        var raw = new[] { "1.0.10", "1.0.2", "0.9.9", "2.0.0", "1.0.0", "1.10.0" };
        var versions = new List<SemanticVersion>();
        foreach (var item in raw)
        {
            Assert.True(SemanticVersion.TryParse(item, out var parsed));
            versions.Add(parsed);
        }

        versions.Sort();

        Assert.Equal(
            ["0.9.9", "1.0.0", "1.0.2", "1.0.10", "1.10.0", "2.0.0"],
            versions.Select(version => version.ToString()));
    }

    /// <summary>
    /// 用字符串比较器排序脚本版本目录名时同样保持语义顺序
    /// </summary>
    [Fact]
    public void OrderBy_WithCompareAsComparer_OrdersVersionFoldersSemantically()
    {
        var folders = new[] { "1.0.10", "0.9.0", "1.0.2", "1.0.0" };

        var ordered = folders
            .OrderBy(folder => folder, Comparer<string>.Create((x, y) => SemanticVersion.Compare(x, y)))
            .ToArray();

        Assert.Equal(["0.9.0", "1.0.0", "1.0.2", "1.0.10"], ordered);
    }
}

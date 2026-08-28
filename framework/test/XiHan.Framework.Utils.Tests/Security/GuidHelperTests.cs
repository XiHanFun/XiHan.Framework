// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// Guid 辅助类测试
/// </summary>
/// <remarks>
/// <para>
/// 版本位与变体位的落点是 RFC 4122 的硬约定：在 .NET 的 <see cref="Guid.ToByteArray()"/>
/// 小端布局里，版本位在下标 7 的高半字节，变体位在下标 8 的高两位——
/// 这正是 <see cref="GuidHelper.GetVersion"/> / <see cref="GuidHelper.GetVariant"/> 读取的位置。
/// </para>
/// <para>
/// 确定性 Guid 的核心契约是「同输入必得同输出、不同命名空间必得不同输出」，这部分被完整锁死。
/// </para>
/// </remarks>
public class GuidHelperTests
{
    private static readonly Guid SampleGuid = new("2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7");

    /// <summary>
    /// 标准随机 Guid 非空且互不相同
    /// </summary>
    [Fact]
    public void NewGuid_ProducesNonEmptyUniqueValues()
    {
        var guids = new HashSet<Guid>();
        for (var i = 0; i < 200; i++)
        {
            var guid = GuidHelper.NewGuid();
            Assert.NotEqual(Guid.Empty, guid);
            guids.Add(guid);
        }

        Assert.Equal(200, guids.Count);
    }

    /// <summary>
    /// 加密安全 Guid 的版本位为 4、变体位为 2
    /// </summary>
    [Fact]
    public void NewCryptoGuid_SetsVersion4AndRfcVariant()
    {
        for (var i = 0; i < 50; i++)
        {
            var guid = GuidHelper.NewCryptoGuid();

            Assert.Equal(4, GuidHelper.GetVersion(guid));
            Assert.Equal(2, GuidHelper.GetVariant(guid));
        }
    }

    /// <summary>
    /// 时间有序 Guid 的版本位为 1、变体位为 2
    /// </summary>
    [Fact]
    public void NewTimeBasedGuid_SetsVersion1AndRfcVariant()
    {
        for (var i = 0; i < 50; i++)
        {
            var guid = GuidHelper.NewTimeBasedGuid();

            Assert.Equal(1, GuidHelper.GetVersion(guid));
            Assert.Equal(2, GuidHelper.GetVariant(guid));
        }
    }

    /// <summary>
    /// 时间有序 Guid 互不相同
    /// </summary>
    [Fact]
    public void NewTimeBasedGuid_ProducesUniqueValues()
    {
        var guids = new HashSet<Guid>();
        for (var i = 0; i < 200; i++)
        {
            guids.Add(GuidHelper.NewTimeBasedGuid());
        }

        Assert.Equal(200, guids.Count);
    }

    /// <summary>
    /// SHA1 确定性 Guid：同输入同命名空间恒定产出同一值
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_ForSameInput_IsStable()
    {
        Assert.Equal(GuidHelper.NewDeterministicGuid("order-1024"), GuidHelper.NewDeterministicGuid("order-1024"));
    }

    /// <summary>
    /// SHA1 确定性 Guid：不同输入产出不同值
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_ForDifferentInput_ProducesDifferentValue()
    {
        Assert.NotEqual(GuidHelper.NewDeterministicGuid("order-1024"), GuidHelper.NewDeterministicGuid("order-1025"));
    }

    /// <summary>
    /// SHA1 确定性 Guid：命名空间参与派生，换命名空间必然换结果
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_WithDifferentNamespace_ProducesDifferentValue()
    {
        var withDefaultNamespace = GuidHelper.NewDeterministicGuid("order-1024");
        var withCustomNamespace = GuidHelper.NewDeterministicGuid("order-1024", SampleGuid);

        Assert.NotEqual(withDefaultNamespace, withCustomNamespace);
        Assert.Equal(withCustomNamespace, GuidHelper.NewDeterministicGuid("order-1024", SampleGuid));
    }

    /// <summary>
    /// SHA1 确定性 Guid 的变体位为 2
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_SetsRfcVariant()
    {
        Assert.Equal(2, GuidHelper.GetVariant(GuidHelper.NewDeterministicGuid("order-1024")));
    }

    /// <summary>
    /// SHA1 确定性 Guid 的版本位应为 5（基于名称的 SHA1）
    /// </summary>
    /// <remarks>
    /// 【已知红灯 / 疑似缺陷】实现把版本位写在 <c>guidBytes[6]</c>，
    /// 而 RFC 4122 的版本位在 .NET 小端布局的 <c>bytes[7]</c> 高半字节，
    /// 与本类自己的 <see cref="GuidHelper.GetVersion"/> 读取位置不一致，
    /// 结果是这里读到的是哈希残留的随机值而不是 5。
    /// 按 RFC 与代码注释宣称的语义断言，缺陷已上报由主控裁决。
    /// </remarks>
    [Fact]
    public void NewDeterministicGuid_SetsVersion5()
    {
        Assert.Equal(5, GuidHelper.GetVersion(GuidHelper.NewDeterministicGuid("order-1024")));
    }

    /// <summary>
    /// MD5 确定性 Guid：同输入恒定、不同输入相异，且与 SHA1 版本结果不同
    /// </summary>
    [Fact]
    public void NewDeterministicGuidMd5_IsStableAndDistinctFromSha1Variant()
    {
        Assert.Equal(GuidHelper.NewDeterministicGuidMd5("order-1024"), GuidHelper.NewDeterministicGuidMd5("order-1024"));
        Assert.NotEqual(GuidHelper.NewDeterministicGuidMd5("order-1024"), GuidHelper.NewDeterministicGuidMd5("order-1025"));
        Assert.NotEqual(GuidHelper.NewDeterministicGuidMd5("order-1024"), GuidHelper.NewDeterministicGuid("order-1024"));
    }

    /// <summary>
    /// 确定性 Guid 的输入为 null 时抛空引用参数异常
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_WhenInputNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.NewDeterministicGuid(null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.NewDeterministicGuidMd5(null!); });
    }

    /// <summary>
    /// 中文输入同样能稳定派生
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_ForChineseInput_IsStable()
    {
        Assert.Equal(GuidHelper.NewDeterministicGuid("订单-1024"), GuidHelper.NewDeterministicGuid("订单-1024"));
        Assert.NotEqual(GuidHelper.NewDeterministicGuid("订单-1024"), GuidHelper.NewDeterministicGuid("订单-1025"));
    }

    /// <summary>
    /// Guid 有效性判定
    /// </summary>
    [Theory]
    [InlineData("2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7", true)]
    [InlineData("2f8a1c345b6d4e7f8091a2b3c4d5e6f7", true)]
    [InlineData("{2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7}", true)]
    [InlineData("not-a-guid", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidGuid_ClassifiesInput(string input, bool expected)
    {
        Assert.Equal(expected, GuidHelper.IsValidGuid(input));
    }

    /// <summary>
    /// 标准带连字符格式判定
    /// </summary>
    [Theory]
    [InlineData("2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7", true)]
    [InlineData("2F8A1C34-5B6D-4E7F-8091-A2B3C4D5E6F7", true)]
    [InlineData("2f8a1c345b6d4e7f8091a2b3c4d5e6f7", false)]
    [InlineData("{2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7}", false)]
    [InlineData("", false)]
    public void IsValidStandardGuid_RequiresDashedForm(string input, bool expected)
    {
        Assert.Equal(expected, GuidHelper.IsValidStandardGuid(input));
    }

    /// <summary>
    /// 无连字符格式判定
    /// </summary>
    [Theory]
    [InlineData("2f8a1c345b6d4e7f8091a2b3c4d5e6f7", true)]
    [InlineData("2F8A1C345B6D4E7F8091A2B3C4D5E6F7", true)]
    [InlineData("2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7", false)]
    [InlineData("2f8a1c345b6d4e7f8091a2b3c4d5e6", false)]
    [InlineData("", false)]
    public void IsValidGuidNoDash_RequiresThirtyTwoHexChars(string input, bool expected)
    {
        Assert.Equal(expected, GuidHelper.IsValidGuidNoDash(input));
    }

    /// <summary>
    /// 尝试解析成功与失败两条路径
    /// </summary>
    [Fact]
    public void TryParse_ReturnsParsedGuidOnlyOnSuccess()
    {
        Assert.True(GuidHelper.TryParse(SampleGuid.ToString("D"), out var parsed));
        Assert.Equal(SampleGuid, parsed);

        Assert.False(GuidHelper.TryParse("not-a-guid", out var failed));
        Assert.Equal(Guid.Empty, failed);
    }

    /// <summary>
    /// 解析失败时抛格式异常，入参为 null 时抛空引用参数异常
    /// </summary>
    [Fact]
    public void Parse_ThrowsOnInvalidInput()
    {
        Assert.Equal(SampleGuid, GuidHelper.Parse(SampleGuid.ToString("D")));
        Assert.Throws<FormatException>(() => { _ = GuidHelper.Parse("not-a-guid"); });
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.Parse(null!); });
    }

    /// <summary>
    /// 五种格式化输出与 <see cref="Guid.ToString(string)"/> 一致
    /// </summary>
    [Theory]
    [InlineData("N")]
    [InlineData("D")]
    [InlineData("B")]
    [InlineData("P")]
    [InlineData("X")]
    public void ToString_MatchesGuidFormatting(string format)
    {
        Assert.Equal(SampleGuid.ToString(format), GuidHelper.ToString(SampleGuid, format));
    }

    /// <summary>
    /// 无连字符输出与大小写转换
    /// </summary>
    [Fact]
    public void ToStringVariants_ProduceExpectedCasing()
    {
        Assert.Equal("2f8a1c345b6d4e7f8091a2b3c4d5e6f7", GuidHelper.ToStringNoDash(SampleGuid));
        Assert.Equal("2F8A1C34-5B6D-4E7F-8091-A2B3C4D5E6F7", GuidHelper.ToUpperString(SampleGuid));
        Assert.Equal("2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7", GuidHelper.ToLowerString(SampleGuid));
        Assert.Equal("2F8A1C345B6D4E7F8091A2B3C4D5E6F7", GuidHelper.ToUpperString(SampleGuid, "N"));
    }

    /// <summary>
    /// 两种字符串格式互转
    /// </summary>
    [Fact]
    public void FormatConversions_RoundTrip()
    {
        var standard = SampleGuid.ToString("D");
        var noDash = SampleGuid.ToString("N");

        Assert.Equal(standard, GuidHelper.ToStandardFormat(noDash));
        Assert.Equal(noDash, GuidHelper.ToNoDashFormat(standard));
    }

    /// <summary>
    /// 格式不匹配时拒绝转换
    /// </summary>
    [Fact]
    public void FormatConversions_RejectWrongForm()
    {
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.ToStandardFormat(SampleGuid.ToString("D")); });
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.ToNoDashFormat(SampleGuid.ToString("N")); });
    }

    /// <summary>
    /// 字节数组互转往返
    /// </summary>
    [Fact]
    public void ByteArrayConversions_RoundTrip()
    {
        var bytes = GuidHelper.ToByteArray(SampleGuid);

        Assert.Equal(16, bytes.Length);
        Assert.Equal(SampleGuid, GuidHelper.FromByteArray(bytes));
    }

    /// <summary>
    /// 字节数组长度不为 16 时拒绝构造
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    public void FromByteArray_WithWrongLength_ThrowsArgumentException(int length)
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = GuidHelper.FromByteArray(new byte[length]); });

        Assert.Contains("16", exception.Message);
    }

    /// <summary>
    /// 字节数组为 null 时抛空引用参数异常
    /// </summary>
    [Fact]
    public void FromByteArray_WhenNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.FromByteArray(null!); });
    }

    /// <summary>
    /// Base64 互转往返
    /// </summary>
    [Fact]
    public void Base64Conversions_RoundTrip()
    {
        var base64 = GuidHelper.ToBase64String(SampleGuid);

        Assert.Equal(24, base64.Length);
        Assert.Equal(SampleGuid, GuidHelper.FromBase64String(base64));
    }

    /// <summary>
    /// Base64 非法或长度不符时抛参数异常
    /// </summary>
    [Fact]
    public void FromBase64String_WithInvalidInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.FromBase64String("not base64 @@@"); });
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.FromBase64String(Convert.ToBase64String(new byte[8])); });
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.FromBase64String(null!); });
    }

    /// <summary>
    /// 空值判定
    /// </summary>
    [Fact]
    public void EmptyChecks_ReflectGuidEmpty()
    {
        Assert.True(GuidHelper.IsEmpty(Guid.Empty));
        Assert.False(GuidHelper.IsNotEmpty(Guid.Empty));
        Assert.False(GuidHelper.IsEmpty(SampleGuid));
        Assert.True(GuidHelper.IsNotEmpty(SampleGuid));
    }

    /// <summary>
    /// 相等比较与哈希码转发到 <see cref="Guid"/> 自身实现
    /// </summary>
    [Fact]
    public void EqualityHelpers_DelegateToGuid()
    {
        Assert.True(GuidHelper.AreEqual(SampleGuid, new Guid(SampleGuid.ToString("D"))));
        Assert.False(GuidHelper.AreEqual(SampleGuid, Guid.Empty));
        Assert.Equal(SampleGuid.GetHashCode(), GuidHelper.GetHashCode(SampleGuid));
    }

    /// <summary>
    /// 批量生成指定数量且互不相同
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenerateMultiple_ProducesRequestedCountOfDistinctGuids(bool useCrypto)
    {
        var guids = GuidHelper.GenerateMultiple(50, useCrypto);

        Assert.Equal(50, guids.Count);
        Assert.Equal(50, new HashSet<Guid>(guids).Count);
    }

    /// <summary>
    /// 批量生成数量为 0 时返回空列表
    /// </summary>
    [Fact]
    public void GenerateMultiple_WithZeroCount_ReturnsEmptyList()
    {
        Assert.Empty(GuidHelper.GenerateMultiple(0));
    }

    /// <summary>
    /// 批量生成数量为负时抛参数异常
    /// </summary>
    [Fact]
    public void GenerateMultiple_WithNegativeCount_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.GenerateMultiple(-1); });
    }

    /// <summary>
    /// 格式示例字典覆盖六种表示且内容与格式化结果一致
    /// </summary>
    [Fact]
    public void GetFormatExamples_CoversAllSupportedForms()
    {
        var examples = GuidHelper.GetFormatExamples(SampleGuid);

        Assert.Equal(6, examples.Count);
        Assert.Equal(SampleGuid.ToString("N"), examples["N"]);
        Assert.Equal(SampleGuid.ToString("D"), examples["D"]);
        Assert.Equal(SampleGuid.ToString("B"), examples["B"]);
        Assert.Equal(SampleGuid.ToString("P"), examples["P"]);
        Assert.Equal(SampleGuid.ToString("X"), examples["X"]);
        Assert.Equal(GuidHelper.ToBase64String(SampleGuid), examples["Base64"]);
    }

    /// <summary>
    /// 不传 Guid 时使用新生成的值，六个键仍然齐全
    /// </summary>
    [Fact]
    public void GetFormatExamples_WithoutGuid_UsesNewlyGeneratedValue()
    {
        var first = GuidHelper.GetFormatExamples();
        var second = GuidHelper.GetFormatExamples();

        Assert.Equal(6, first.Count);
        Assert.NotEqual(first["D"], second["D"]);
    }
}

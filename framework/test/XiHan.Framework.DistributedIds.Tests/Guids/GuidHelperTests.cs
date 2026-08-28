// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Guids;

namespace XiHan.Framework.DistributedIds.Tests.Guids;

/// <summary>
/// Guid 生成与操作辅助类的测试
/// </summary>
/// <remarks>
/// 覆盖生成（随机 / 加密随机 / 时间序 / 确定性）、校验、格式互转、字节与 Base64 互转四组契约，
/// 以及所有会抛异常的参数校验分支。
/// </remarks>
public class GuidHelperTests
{
    /// <summary>
    /// 随机 Guid 连续生成不重复
    /// </summary>
    [Fact]
    public void NewGuid_ProducesDistinctValues()
    {
        var guids = Enumerable.Range(0, 100).Select(_ => GuidHelper.NewGuid()).ToArray();

        Assert.Equal(100, guids.Distinct().Count());
        Assert.All(guids, guid => Assert.NotEqual(Guid.Empty, guid));
    }

    /// <summary>
    /// 加密安全 Guid 带上版本 4 与标准变体位
    /// </summary>
    [Fact]
    public void NewCryptoGuid_HasVersionFourAndStandardVariant()
    {
        var guid = GuidHelper.NewCryptoGuid();

        Assert.NotEqual(Guid.Empty, guid);
        Assert.Equal(4, GuidHelper.GetVersion(guid));
        Assert.Equal(2, GuidHelper.GetVariant(guid));
    }

    /// <summary>
    /// 时间序 Guid 带上版本 1 与标准变体位
    /// </summary>
    [Fact]
    public void NewTimeBasedGuid_HasVersionOneAndStandardVariant()
    {
        var guid = GuidHelper.NewTimeBasedGuid();

        Assert.NotEqual(Guid.Empty, guid);
        Assert.Equal(1, GuidHelper.GetVersion(guid));
        Assert.Equal(2, GuidHelper.GetVariant(guid));
    }

    /// <summary>
    /// 时间序 Guid 连续生成不重复
    /// </summary>
    [Fact]
    public void NewTimeBasedGuid_ProducesDistinctValues()
    {
        var guids = Enumerable.Range(0, 50).Select(_ => GuidHelper.NewTimeBasedGuid()).ToArray();

        Assert.Equal(50, guids.Distinct().Count());
    }

    /// <summary>
    /// 相同输入的确定性 Guid 必须完全一致
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_SameInput_ProducesSameValue()
    {
        var first = GuidHelper.NewDeterministicGuid("xihan-framework");
        var second = GuidHelper.NewDeterministicGuid("xihan-framework");

        Assert.Equal(first, second);
        Assert.Equal(2, GuidHelper.GetVariant(first));
    }

    /// <summary>
    /// 不同输入或不同命名空间的确定性 Guid 必须不同
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_DifferentInputOrNamespace_ProducesDifferentValue()
    {
        var baseline = GuidHelper.NewDeterministicGuid("xihan-framework");
        var otherInput = GuidHelper.NewDeterministicGuid("xihan-framework-2");
        var otherNamespace = GuidHelper.NewDeterministicGuid("xihan-framework", Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"));

        Assert.NotEqual(baseline, otherInput);
        Assert.NotEqual(baseline, otherNamespace);
    }

    /// <summary>
    /// 确定性 Guid 的输入为空时直接失败
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_WhenInputNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.NewDeterministicGuid(null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.NewDeterministicGuidMd5(null!); });
    }

    /// <summary>
    /// MD5 版本的确定性 Guid 同样可重现，且与 SHA1 版本不同
    /// </summary>
    [Fact]
    public void NewDeterministicGuidMd5_IsReproducibleAndDiffersFromSha1()
    {
        var first = GuidHelper.NewDeterministicGuidMd5("xihan-framework");
        var second = GuidHelper.NewDeterministicGuidMd5("xihan-framework");
        var sha1 = GuidHelper.NewDeterministicGuid("xihan-framework");

        Assert.Equal(first, second);
        Assert.NotEqual(first, sha1);
        Assert.Equal(2, GuidHelper.GetVariant(first));
    }

    /// <summary>
    /// 通用校验接受带连字符与不带连字符两种写法
    /// </summary>
    [Fact]
    public void IsValidGuid_AcceptsBothDashedAndDashlessForms()
    {
        var guid = Guid.NewGuid();

        Assert.True(GuidHelper.IsValidGuid(guid.ToString("D")));
        Assert.True(GuidHelper.IsValidGuid(guid.ToString("N")));
    }

    /// <summary>
    /// 通用校验拒绝空白与非法文本
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData(null)]
    public void IsValidGuid_RejectsBlankAndMalformed(string? candidate)
    {
        Assert.False(GuidHelper.IsValidGuid(candidate!));
    }

    /// <summary>
    /// 标准格式校验只接受带连字符的写法
    /// </summary>
    [Fact]
    public void IsValidStandardGuid_OnlyAcceptsDashedForm()
    {
        var guid = Guid.NewGuid();

        Assert.True(GuidHelper.IsValidStandardGuid(guid.ToString("D")));
        Assert.False(GuidHelper.IsValidStandardGuid(guid.ToString("N")));
        Assert.False(GuidHelper.IsValidStandardGuid(guid.ToString("B")));
        Assert.False(GuidHelper.IsValidStandardGuid("   "));
    }

    /// <summary>
    /// 无连字符校验只接受 32 位十六进制写法
    /// </summary>
    [Fact]
    public void IsValidGuidNoDash_OnlyAcceptsDashlessForm()
    {
        var guid = Guid.NewGuid();

        Assert.True(GuidHelper.IsValidGuidNoDash(guid.ToString("N")));
        Assert.False(GuidHelper.IsValidGuidNoDash(guid.ToString("D")));
        Assert.False(GuidHelper.IsValidGuidNoDash("   "));
    }

    /// <summary>
    /// 尝试解析成功时输出原值，失败时输出空 Guid
    /// </summary>
    [Fact]
    public void TryParse_ReportsSuccessAndFailure()
    {
        var guid = Guid.NewGuid();

        Assert.True(GuidHelper.TryParse(guid.ToString(), out var parsed));
        Assert.Equal(guid, parsed);

        Assert.False(GuidHelper.TryParse("not-a-guid", out var failed));
        Assert.Equal(Guid.Empty, failed);
    }

    /// <summary>
    /// 强制解析在输入为空或格式非法时抛出对应异常
    /// </summary>
    [Fact]
    public void Parse_RejectsNullAndMalformed()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(guid, GuidHelper.Parse(guid.ToString()));
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.Parse(null!); });
        Assert.Throws<FormatException>(() => { _ = GuidHelper.Parse("not-a-guid"); });
    }

    /// <summary>
    /// 格式化默认走 D 格式，并支持其余标准格式
    /// </summary>
    [Fact]
    public void ToString_DefaultsToDashedFormat()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(guid.ToString("D"), GuidHelper.ToString(guid));
        Assert.Equal(guid.ToString("N"), GuidHelper.ToString(guid, "N"));
        Assert.Equal(guid.ToString("B"), GuidHelper.ToString(guid, "B"));
        Assert.Equal(guid.ToString("P"), GuidHelper.ToString(guid, "P"));
        Assert.Equal(guid.ToString("X"), GuidHelper.ToString(guid, "X"));
    }

    /// <summary>
    /// 无连字符格式化去掉全部连字符且长度为 32
    /// </summary>
    [Fact]
    public void ToStringNoDash_RemovesDashes()
    {
        var text = GuidHelper.ToStringNoDash(Guid.NewGuid());

        Assert.Equal(32, text.Length);
        Assert.DoesNotContain("-", text);
    }

    /// <summary>
    /// 大小写格式化只改变大小写，不改变内容
    /// </summary>
    [Fact]
    public void ToUpperAndToLowerString_OnlyChangeCasing()
    {
        var guid = Guid.NewGuid();

        var upper = GuidHelper.ToUpperString(guid);
        var lower = GuidHelper.ToLowerString(guid);

        Assert.Equal(guid.ToString("D").ToUpperInvariant(), upper);
        Assert.Equal(guid.ToString("D").ToLowerInvariant(), lower);
        Assert.Equal(guid, Guid.Parse(upper));
        Assert.Equal(guid, Guid.Parse(lower));
    }

    /// <summary>
    /// 无连字符与标准格式之间可以互转
    /// </summary>
    [Fact]
    public void FormatConversions_RoundTrip()
    {
        var guid = Guid.NewGuid();

        var standard = GuidHelper.ToStandardFormat(guid.ToString("N"));
        var noDash = GuidHelper.ToNoDashFormat(standard);

        Assert.Equal(guid.ToString("D"), standard);
        Assert.Equal(guid.ToString("N"), noDash);
    }

    /// <summary>
    /// 格式互转在输入格式不匹配时拒绝转换
    /// </summary>
    [Fact]
    public void FormatConversions_RejectMismatchedInput()
    {
        var guid = Guid.NewGuid();

        // 带连字符的串不能当成无连字符格式
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.ToStandardFormat(guid.ToString("D")); });
        // 无连字符的串不能当成标准格式
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.ToNoDashFormat(guid.ToString("N")); });
    }

    /// <summary>
    /// 字节数组与 Guid 之间可以互转
    /// </summary>
    [Fact]
    public void ByteArrayConversions_RoundTrip()
    {
        var guid = Guid.NewGuid();

        var bytes = GuidHelper.ToByteArray(guid);

        Assert.Equal(16, bytes.Length);
        Assert.Equal(guid, GuidHelper.FromByteArray(bytes));
    }

    /// <summary>
    /// 字节数组为空或长度不是 16 时拒绝转换
    /// </summary>
    [Fact]
    public void FromByteArray_RejectsNullAndWrongLength()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.FromByteArray(null!); });
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.FromByteArray(new byte[15]); });
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.FromByteArray(new byte[17]); });
    }

    /// <summary>
    /// 空 Guid 的版本号为 0
    /// </summary>
    [Fact]
    public void GetVersion_OnEmptyGuid_IsZero()
    {
        Assert.Equal(0, GuidHelper.GetVersion(Guid.Empty));
    }

    /// <summary>
    /// 空判断与非空判断互为反义
    /// </summary>
    [Fact]
    public void IsEmptyAndIsNotEmpty_AreComplementary()
    {
        var guid = Guid.NewGuid();

        Assert.True(GuidHelper.IsEmpty(Guid.Empty));
        Assert.False(GuidHelper.IsNotEmpty(Guid.Empty));
        Assert.False(GuidHelper.IsEmpty(guid));
        Assert.True(GuidHelper.IsNotEmpty(guid));
    }

    /// <summary>
    /// 批量生成返回请求数量且互不重复
    /// </summary>
    [Fact]
    public void GenerateMultiple_ReturnsRequestedCount()
    {
        var guids = GuidHelper.GenerateMultiple(20);

        Assert.Equal(20, guids.Count);
        Assert.Equal(20, guids.Distinct().Count());
    }

    /// <summary>
    /// 批量生成加密安全 Guid 时每一个都是版本 4
    /// </summary>
    [Fact]
    public void GenerateMultiple_WithCrypto_ProducesVersionFourGuids()
    {
        var guids = GuidHelper.GenerateMultiple(10, useCrypto: true);

        Assert.Equal(10, guids.Count);
        Assert.All(guids, guid => Assert.Equal(4, GuidHelper.GetVersion(guid)));
    }

    /// <summary>
    /// 数量为 0 时返回空集合，为负时直接失败
    /// </summary>
    [Fact]
    public void GenerateMultiple_HandlesZeroAndNegativeCount()
    {
        Assert.Empty(GuidHelper.GenerateMultiple(0));
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.GenerateMultiple(-1); });
    }

    /// <summary>
    /// 相等判断与哈希码转发到 Guid 自身语义
    /// </summary>
    [Fact]
    public void AreEqualAndGetHashCode_MatchGuidSemantics()
    {
        var guid = Guid.NewGuid();
        var copy = Guid.Parse(guid.ToString());

        Assert.True(GuidHelper.AreEqual(guid, copy));
        Assert.False(GuidHelper.AreEqual(guid, Guid.NewGuid()));
        Assert.Equal(guid.GetHashCode(), GuidHelper.GetHashCode(guid));
    }

    /// <summary>
    /// Base64 与 Guid 之间可以互转
    /// </summary>
    [Fact]
    public void Base64Conversions_RoundTrip()
    {
        var guid = Guid.NewGuid();

        var base64 = GuidHelper.ToBase64String(guid);

        Assert.Equal(24, base64.Length);
        Assert.Equal(guid, GuidHelper.FromBase64String(base64));
    }

    /// <summary>
    /// Base64 为空、非法或长度不足时拒绝转换
    /// </summary>
    [Fact]
    public void FromBase64String_RejectsNullAndMalformed()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = GuidHelper.FromBase64String(null!); });
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.FromBase64String("!!!不是 Base64!!!"); });
        // 合法 Base64 但解出来只有 8 字节，凑不满一个 Guid
        Assert.Throws<ArgumentException>(() => { _ = GuidHelper.FromBase64String(Convert.ToBase64String(new byte[8])); });
    }

    /// <summary>
    /// 格式示例字典覆盖全部受支持的格式
    /// </summary>
    [Fact]
    public void GetFormatExamples_CoversAllSupportedFormats()
    {
        var guid = Guid.NewGuid();

        var examples = GuidHelper.GetFormatExamples(guid);

        Assert.Equal(6, examples.Count);
        Assert.Equal(guid.ToString("N"), examples["N"]);
        Assert.Equal(guid.ToString("D"), examples["D"]);
        Assert.Equal(guid.ToString("B"), examples["B"]);
        Assert.Equal(guid.ToString("P"), examples["P"]);
        Assert.Equal(guid.ToString("X"), examples["X"]);
        Assert.Equal(Convert.ToBase64String(guid.ToByteArray()), examples["Base64"]);
    }

    /// <summary>
    /// 不传示例 Guid 时自动生成一个，键集合保持一致
    /// </summary>
    [Fact]
    public void GetFormatExamples_WithoutArgument_GeneratesSample()
    {
        var examples = GuidHelper.GetFormatExamples();

        Assert.Equal(6, examples.Count);
        Assert.True(GuidHelper.IsValidStandardGuid(examples["D"]));
        Assert.True(GuidHelper.IsValidGuidNoDash(examples["N"]));
    }
}

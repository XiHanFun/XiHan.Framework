// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// HMAC 辅助类测试
/// </summary>
/// <remarks>
/// 字符串重载返回 Base64、字节重载返回原始字节，这两条口径被开放接口验签直接依赖，必须锁死。
/// 固定向量取自公开的 HMAC 测试用例（key = "key"，data = "The quick brown fox jumps over the lazy dog"）。
/// </remarks>
public class HmacHelperTests
{
    private const string VectorKey = "key";
    private const string VectorData = "The quick brown fox jumps over the lazy dog";

    /// <summary>
    /// HmacSha1 固定向量（十六进制）
    /// </summary>
    [Fact]
    public void HmacSha1_ForKnownKeyAndData_MatchesPublishedVector()
    {
        var mac = HmacHelper.HmacSha1(Encoding.UTF8.GetBytes(VectorKey), Encoding.UTF8.GetBytes(VectorData));

        Assert.Equal("DE7C9B85B8B78AA6BC8A7A36F70A90701C9DB4D9", Convert.ToHexString(mac));
    }

    /// <summary>
    /// HmacSha256 固定向量（十六进制）
    /// </summary>
    [Fact]
    public void HmacSha256_ForKnownKeyAndData_MatchesPublishedVector()
    {
        var mac = HmacHelper.HmacSha256(Encoding.UTF8.GetBytes(VectorKey), Encoding.UTF8.GetBytes(VectorData));

        Assert.Equal(
            "F7BC83F430538424B13298E6AA6FB143EF4D59A14946175997479DBC2D1A3CD8",
            Convert.ToHexString(mac));
    }

    /// <summary>
    /// 字符串重载返回的是字节重载结果的 Base64 编码
    /// </summary>
    [Theory]
    [InlineData("HMACSHA1")]
    [InlineData("HMACSHA256")]
    [InlineData("HMACSHA384")]
    [InlineData("HMACSHA512")]
    public void ComputeHmac_ReturnsBase64OfComputeHmacBytes(string algorithm)
    {
        var expected = Convert.ToBase64String(
            HmacHelper.ComputeHmacBytes(algorithm, Encoding.UTF8.GetBytes(VectorKey), Encoding.UTF8.GetBytes(VectorData)));

        Assert.Equal(expected, HmacHelper.ComputeHmac(algorithm, VectorKey, VectorData));
    }

    /// <summary>
    /// 四个具名方法与对应算法名的通用方法结果一致
    /// </summary>
    [Fact]
    public void NamedMethods_MatchGenericComputeHmac()
    {
        Assert.Equal(HmacHelper.ComputeHmac("HMACSHA1", VectorKey, VectorData), HmacHelper.HmacSha1(VectorKey, VectorData));
        Assert.Equal(HmacHelper.ComputeHmac("HMACSHA256", VectorKey, VectorData), HmacHelper.HmacSha256(VectorKey, VectorData));
        Assert.Equal(HmacHelper.ComputeHmac("HMACSHA384", VectorKey, VectorData), HmacHelper.HmacSha384(VectorKey, VectorData));
        Assert.Equal(HmacHelper.ComputeHmac("HMACSHA512", VectorKey, VectorData), HmacHelper.HmacSha512(VectorKey, VectorData));
    }

    /// <summary>
    /// 各算法输出的字节长度符合摘要位宽
    /// </summary>
    [Theory]
    [InlineData("HMACSHA1", 20)]
    [InlineData("HMACSHA256", 32)]
    [InlineData("HMACSHA384", 48)]
    [InlineData("HMACSHA512", 64)]
    public void ComputeHmacBytes_ProducesDigestSizedOutput(string algorithm, int expectedLength)
    {
        var mac = HmacHelper.ComputeHmacBytes(algorithm, Encoding.UTF8.GetBytes(VectorKey), Encoding.UTF8.GetBytes(VectorData));

        Assert.Equal(expectedLength, mac.Length);
    }

    /// <summary>
    /// 同样的密钥与消息重复计算结果稳定
    /// </summary>
    [Fact]
    public void HmacSha256_ForSameInput_IsDeterministic()
    {
        Assert.Equal(HmacHelper.HmacSha256(VectorKey, VectorData), HmacHelper.HmacSha256(VectorKey, VectorData));
    }

    /// <summary>
    /// 密钥不同则 MAC 不同
    /// </summary>
    [Fact]
    public void HmacSha256_WithDifferentKey_ProducesDifferentMac()
    {
        Assert.NotEqual(HmacHelper.HmacSha256("key-a", VectorData), HmacHelper.HmacSha256("key-b", VectorData));
    }

    /// <summary>
    /// 消息不同则 MAC 不同
    /// </summary>
    [Fact]
    public void HmacSha256_WithDifferentMessage_ProducesDifferentMac()
    {
        Assert.NotEqual(HmacHelper.HmacSha256(VectorKey, "message-a"), HmacHelper.HmacSha256(VectorKey, "message-b"));
    }

    /// <summary>
    /// 中文密钥与中文消息按 UTF-8 编码
    /// </summary>
    [Fact]
    public void HmacSha256_ForChinese_UsesUtf8Encoding()
    {
        const string ChineseKey = "曦寒密钥";
        const string ChineseMessage = "曦寒框架·中文消息";

        var expected = Convert.ToBase64String(
            HmacHelper.HmacSha256(Encoding.UTF8.GetBytes(ChineseKey), Encoding.UTF8.GetBytes(ChineseMessage)));

        Assert.Equal(expected, HmacHelper.HmacSha256(ChineseKey, ChineseMessage));
    }

    /// <summary>
    /// 空消息是合法输入，仍然产出定长 MAC
    /// </summary>
    [Fact]
    public void HmacSha256_ForEmptyMessage_ReturnsDigestSizedMac()
    {
        var mac = Convert.FromBase64String(HmacHelper.HmacSha256(VectorKey, string.Empty));

        Assert.Equal(32, mac.Length);
    }

    /// <summary>
    /// 超长消息不会截断，且与逐字节口径一致
    /// </summary>
    [Fact]
    public void HmacSha256_ForVeryLongMessage_MatchesByteOverload()
    {
        var longMessage = new string('z', 500_000);

        var expected = Convert.ToBase64String(
            HmacHelper.HmacSha256(Encoding.UTF8.GetBytes(VectorKey), Encoding.UTF8.GetBytes(longMessage)));

        Assert.Equal(expected, HmacHelper.HmacSha256(VectorKey, longMessage));
    }

    /// <summary>
    /// 算法名为空时抛出参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ComputeHmac_WhenAlgorithmBlank_ThrowsArgumentException(string algorithm)
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = HmacHelper.ComputeHmac(algorithm, VectorKey, VectorData); });

        Assert.Contains("算法名称不能为空", exception.Message);
    }

    /// <summary>
    /// 密钥为空时抛出参数异常
    /// </summary>
    [Fact]
    public void ComputeHmac_WhenKeyEmpty_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = HmacHelper.ComputeHmac("HMACSHA256", string.Empty, VectorData); });

        Assert.Contains("密钥不能为空", exception.Message);
    }

    /// <summary>
    /// 字节重载密钥为空数组时抛出参数异常
    /// </summary>
    [Fact]
    public void ComputeHmacBytes_WhenKeyEmpty_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => { _ = HmacHelper.ComputeHmacBytes("HMACSHA256", [], Encoding.UTF8.GetBytes(VectorData)); });

        Assert.Contains("密钥不能为空", exception.Message);
    }

    /// <summary>
    /// 数据为 null 时抛出参数异常
    /// </summary>
    [Fact]
    public void ComputeHmac_WhenDataNull_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = HmacHelper.ComputeHmac("HMACSHA256", VectorKey, null!); });

        Assert.Contains("数据不能为空", exception.Message);
    }

    /// <summary>
    /// 不支持的算法名抛出不支持异常
    /// </summary>
    [Theory]
    [InlineData("HMACMD5")]
    [InlineData("hmacsha256")]
    [InlineData("SHA256")]
    public void ComputeHmac_WhenAlgorithmUnsupported_ThrowsNotSupported(string algorithm)
    {
        Assert.Throws<NotSupportedException>(() => { _ = HmacHelper.ComputeHmac(algorithm, VectorKey, VectorData); });
    }
}

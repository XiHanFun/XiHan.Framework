// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Utils.Tests.Security;

/// <summary>
/// 确定性 Guid 版本位落点回归测试
/// </summary>
/// <remarks>
/// <para>
/// 修复前 <see cref="GuidHelper.NewDeterministicGuid"/> / <see cref="GuidHelper.NewDeterministicGuidMd5"/>
/// 把版本位写进 <c>bytes[6]</c>；而 .NET 的 Guid 字节布局对前三个字段是小端的，
/// Data3 的高字节落在 <c>bytes[7]</c>——RFC 4122 的版本位、本类的 <see cref="GuidHelper.GetVersion"/>、
/// 以及 Guid 字符串第三段的首位十六进制数字读的都是 <c>bytes[7]</c>。
/// </para>
/// <para>
/// 这里除了断言版本号，还钉死若干「固定输入 → 固定输出」向量：版本位落点一旦再次漂移，
/// 或者派生算法（哈希、命名空间拼接顺序）被改动，这些向量会立刻变红。
/// </para>
/// </remarks>
public class GuidHelperDeterministicVersionTests
{
    private static readonly Guid SampleNamespace = new("2f8a1c34-5b6d-4e7f-8091-a2b3c4d5e6f7");

    /// <summary>
    /// SHA1 确定性 Guid 的版本位为 5，且落在 RFC 4122 规定的位置
    /// </summary>
    [Theory]
    [InlineData("order-1024")]
    [InlineData("xihan-framework")]
    [InlineData("订单-1024")]
    [InlineData("")]
    public void NewDeterministicGuid_WritesVersion5IntoRfcVersionNibble(string input)
    {
        var guid = GuidHelper.NewDeterministicGuid(input);

        Assert.Equal(5, GuidHelper.GetVersion(guid));
        Assert.Equal(2, GuidHelper.GetVariant(guid));
        // 字符串第三段的首位就是版本号，第四段的首位落在变体位 10xx 上
        Assert.Equal('5', guid.ToString("D")[14]);
        Assert.Contains(guid.ToString("D")[19].ToString(), "89ab");
        Assert.Equal(0x50, guid.ToByteArray()[7] & 0xF0);
    }

    /// <summary>
    /// MD5 确定性 Guid 的版本位为 3，且落在 RFC 4122 规定的位置
    /// </summary>
    [Theory]
    [InlineData("order-1024")]
    [InlineData("xihan-framework")]
    [InlineData("订单-1024")]
    [InlineData("")]
    public void NewDeterministicGuidMd5_WritesVersion3IntoRfcVersionNibble(string input)
    {
        var guid = GuidHelper.NewDeterministicGuidMd5(input);

        Assert.Equal(3, GuidHelper.GetVersion(guid));
        Assert.Equal(2, GuidHelper.GetVariant(guid));
        Assert.Equal('3', guid.ToString("D")[14]);
        Assert.Contains(guid.ToString("D")[19].ToString(), "89ab");
        Assert.Equal(0x30, guid.ToByteArray()[7] & 0xF0);
    }

    /// <summary>
    /// 指定命名空间时版本位同样落在正确位置
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_WithNamespace_StillWritesVersionNibble()
    {
        Assert.Equal(5, GuidHelper.GetVersion(GuidHelper.NewDeterministicGuid("order-1024", SampleNamespace)));
        Assert.Equal(3, GuidHelper.GetVersion(GuidHelper.NewDeterministicGuidMd5("order-1024", SampleNamespace)));
    }

    /// <summary>
    /// SHA1 确定性 Guid 的固定输入必须得到固定输出
    /// </summary>
    [Theory]
    [InlineData("order-1024", "97f04e98-a2e2-5d83-9616-b64b5e6b45c3")]
    [InlineData("xihan-framework", "fb4e63b7-2726-59fd-a5e8-921152381027")]
    [InlineData("", "7cf229e1-0351-5cbc-844b-cdf0a15e160d")]
    public void NewDeterministicGuid_ForFixedInput_ProducesFixedValue(string input, string expected)
    {
        Assert.Equal(new Guid(expected), GuidHelper.NewDeterministicGuid(input));
    }

    /// <summary>
    /// MD5 确定性 Guid 的固定输入必须得到固定输出
    /// </summary>
    [Theory]
    [InlineData("order-1024", "abd2e2a7-f916-3015-ada4-735f9f3daf81")]
    [InlineData("xihan-framework", "567dfa81-9aa8-3d7d-8eaf-7b21eedc39b1")]
    [InlineData("", "3613e74a-4be4-3ff9-b9d2-752e234818a5")]
    public void NewDeterministicGuidMd5_ForFixedInput_ProducesFixedValue(string input, string expected)
    {
        Assert.Equal(new Guid(expected), GuidHelper.NewDeterministicGuidMd5(input));
    }

    /// <summary>
    /// 带命名空间的固定输入同样得到固定输出
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_WithNamespace_ForFixedInput_ProducesFixedValue()
    {
        Assert.Equal(
            new Guid("bfde3bd4-91a4-55dc-b89f-b7ceffb1d7de"),
            GuidHelper.NewDeterministicGuid("order-1024", SampleNamespace));
        Assert.Equal(
            new Guid("4cbff91f-7faa-3a44-87fa-f3ad297294e5"),
            GuidHelper.NewDeterministicGuidMd5("order-1024", SampleNamespace));
    }

    /// <summary>
    /// 修版本位不能把确定性本身修没：同输入恒定、异输入相异、异命名空间相异
    /// </summary>
    [Fact]
    public void NewDeterministicGuid_KeepsDeterminismAfterVersionFix()
    {
        Assert.Equal(GuidHelper.NewDeterministicGuid("order-1024"), GuidHelper.NewDeterministicGuid("order-1024"));
        Assert.NotEqual(GuidHelper.NewDeterministicGuid("order-1024"), GuidHelper.NewDeterministicGuid("order-1025"));
        Assert.NotEqual(
            GuidHelper.NewDeterministicGuid("order-1024"),
            GuidHelper.NewDeterministicGuid("order-1024", SampleNamespace));
        Assert.NotEqual(GuidHelper.NewDeterministicGuid("order-1024"), GuidHelper.NewDeterministicGuidMd5("order-1024"));
    }

    /// <summary>
    /// 版本位只动高半字节，摘要的其余 15 个字节必须原样保留
    /// </summary>
    /// <remarks>
    /// 防止把「写对位置」修成「顺手洗掉更多熵」：除下标 7 的高半字节与下标 8 的高两位外，
    /// 其余比特都应当来自哈希摘要本身。
    /// </remarks>
    [Fact]
    public void NewDeterministicGuid_OnlyOverwritesVersionAndVariantBits()
    {
        const string Input = "order-1024";
        var combined = Guid.Empty.ToByteArray().Concat(Encoding.UTF8.GetBytes(Input)).ToArray();
        var hash = SHA1.HashData(combined);

        var bytes = GuidHelper.NewDeterministicGuid(Input).ToByteArray();

        for (var i = 0; i < 16; i++)
        {
            var expected = i switch
            {
                7 => (byte)((hash[7] & 0x0F) | 0x50),
                8 => (byte)((hash[8] & 0x3F) | 0x80),
                _ => hash[i]
            };

            Assert.Equal(expected, bytes[i]);
        }
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Security.Cryptography;

namespace XiHan.Framework.Security.Tests.Cryptography;

/// <summary>
/// 国密 SM2 签名/验签的测试
/// </summary>
/// <remarks>
/// 回归保护：Sm2Helper 曾因曲线查找返回 null 导致静态构造崩溃（任何调用即
/// TypeInitializationException），且签名/验签把整段 DER 当作私钥大整数/EC 点，
/// 公开 API 完全不可用。此处按签名/验签契约做完整断言。
/// </remarks>
public class Sm2HelperTests
{
    /// <summary>
    /// 生成的密钥对必须非空、公私不同、且每次生成互不相同
    /// </summary>
    [Fact]
    public void GenerateKeys_ReturnsDistinctNonEmptyKeys()
    {
        var first = Sm2Helper.GenerateKeys();
        var second = Sm2Helper.GenerateKeys();

        Assert.False(string.IsNullOrWhiteSpace(first.publicKey));
        Assert.False(string.IsNullOrWhiteSpace(first.privateKey));
        Assert.NotEqual(first.publicKey, first.privateKey);
        Assert.NotEqual(first.publicKey, second.publicKey);
        Assert.NotEqual(first.privateKey, second.privateKey);
    }

    /// <summary>
    /// 签名与验签往返：合法签名必须验证通过
    /// </summary>
    [Fact]
    public void SignAndVerify_Roundtrip_ReturnsTrue()
    {
        var (publicKey, privateKey) = Sm2Helper.GenerateKeys();
        const string data = "曦寒框架 SM2 签名测试";

        var signature = Sm2Helper.SignData(data, privateKey);

        Assert.False(string.IsNullOrWhiteSpace(signature));
        Assert.True(Sm2Helper.VerifyData(data, signature, publicKey));
    }

    /// <summary>
    /// 篡改数据后验签必须失败
    /// </summary>
    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        var (publicKey, privateKey) = Sm2Helper.GenerateKeys();
        var signature = Sm2Helper.SignData("原始数据", privateKey);

        Assert.False(Sm2Helper.VerifyData("篡改数据", signature, publicKey));
    }

    /// <summary>
    /// 使用其它密钥对的公钥验签必须失败
    /// </summary>
    [Fact]
    public void Verify_WrongPublicKey_ReturnsFalse()
    {
        var (_, privateKey) = Sm2Helper.GenerateKeys();
        var other = Sm2Helper.GenerateKeys();
        var signature = Sm2Helper.SignData("数据", privateKey);

        Assert.False(Sm2Helper.VerifyData("数据", signature, other.publicKey));
    }

    /// <summary>
    /// 篡改签名后验签必须失败
    /// </summary>
    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var (publicKey, privateKey) = Sm2Helper.GenerateKeys();
        var signature = Sm2Helper.SignData("数据", privateKey);
        var bytes = Convert.FromBase64String(signature);
        bytes[0] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        Assert.False(Sm2Helper.VerifyData("数据", tampered, publicKey));
    }

    /// <summary>
    /// 空数据或空密钥必须抛出参数异常
    /// </summary>
    [Theory]
    [InlineData("", "x")]
    [InlineData("data", "")]
    public void Sign_WithBlankArguments_ThrowsArgumentException(string data, string privateKey)
    {
        Assert.Throws<ArgumentException>(() => Sm2Helper.SignData(data, privateKey));
    }
}

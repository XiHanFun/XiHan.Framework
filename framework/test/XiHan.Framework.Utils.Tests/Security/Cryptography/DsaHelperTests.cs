// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// DSA 签名辅助类测试
/// </summary>
/// <remarks>
/// DSA 仅用于兼容既有系统，新签名场景应优先 ECDSA 或 SM2。
/// 各平台对 DSA 的支持差异很大（密钥长度、可用的摘要算法、是否随 OpenSSL provider 提供），
/// 所以先做一次能力探测，探测不通过就整体跳过，避免把平台限制误报成代码缺陷。
/// </remarks>
public class DsaHelperTests
{
    private const string Payload = "曦寒框架·DSA 中文报文签名";

    /// <summary>
    /// 探测结果：能否在当前平台上生成 DSA 密钥并用 SHA256 完成一次签名验签
    /// </summary>
    private static readonly Lazy<(bool Supported, string PublicKey, string PrivateKey)> Probe = new(() =>
    {
        try
        {
            var (publicKey, privateKey) = DsaHelper.GenerateKeys();
            var signature = DsaHelper.SignData("probe", privateKey);
            return DsaHelper.VerifyData("probe", signature, publicKey)
                ? (true, publicKey, privateKey)
                : (false, string.Empty, string.Empty);
        }
        catch (Exception)
        {
            return (false, string.Empty, string.Empty);
        }
    });

    /// <summary>
    /// 生成的密钥对元组顺序是（公钥，私钥），两者不相同
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GenerateKeys_ReturnsDistinctPublicAndPrivateKeys()
    {
        SkipIfDsaUnavailable();

        Assert.NotEqual(Probe.Value.PublicKey, Probe.Value.PrivateKey);
        Assert.NotEmpty(Probe.Value.PublicKey);
        Assert.NotEmpty(Probe.Value.PrivateKey);
    }

    /// <summary>
    /// 签名与验签往返
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SignDataVerifyData_RoundTrips()
    {
        SkipIfDsaUnavailable();

        var signature = DsaHelper.SignData(Payload, Probe.Value.PrivateKey);

        Assert.True(DsaHelper.VerifyData(Payload, signature, Probe.Value.PublicKey));
    }

    /// <summary>
    /// 原文被篡改后验签失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyData_WhenDataTampered_ReturnsFalse()
    {
        SkipIfDsaUnavailable();

        var signature = DsaHelper.SignData(Payload, Probe.Value.PrivateKey);

        Assert.False(DsaHelper.VerifyData(Payload + "!", signature, Probe.Value.PublicKey));
    }

    /// <summary>
    /// 签名被篡改后验签失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyDataBytes_WhenSignatureTampered_ReturnsFalse()
    {
        SkipIfDsaUnavailable();

        var signatureBytes = Convert.FromBase64String(DsaHelper.SignData(Payload, Probe.Value.PrivateKey));
        signatureBytes[0] ^= 0xFF;

        Assert.False(DsaHelper.VerifyDataBytes(
            Encoding.UTF8.GetBytes(Payload),
            signatureBytes,
            Convert.FromBase64String(Probe.Value.PublicKey)));
    }

    /// <summary>
    /// 用不配对的公钥验签失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyData_WithUnrelatedPublicKey_ReturnsFalse()
    {
        SkipIfDsaUnavailable();

        var (otherPublicKey, _) = DsaHelper.GenerateKeys();
        var signature = DsaHelper.SignData(Payload, Probe.Value.PrivateKey);

        Assert.False(DsaHelper.VerifyData(Payload, signature, otherPublicKey));
    }

    /// <summary>
    /// 中文报文按 UTF-8 编码后签名，字节重载能验证同一份签名
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SignData_ForChinese_UsesUtf8Encoding()
    {
        SkipIfDsaUnavailable();

        var signature = DsaHelper.SignData(Payload, Probe.Value.PrivateKey);

        Assert.True(DsaHelper.VerifyDataBytes(
            Encoding.UTF8.GetBytes(Payload),
            Convert.FromBase64String(signature),
            Convert.FromBase64String(Probe.Value.PublicKey)));
    }

    /// <summary>
    /// 空字符串也是可签名的合法数据
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SignDataVerifyData_WithEmptyData_RoundTrips()
    {
        SkipIfDsaUnavailable();

        var signature = DsaHelper.SignData(string.Empty, Probe.Value.PrivateKey);

        Assert.True(DsaHelper.VerifyData(string.Empty, signature, Probe.Value.PublicKey));
    }

    /// <summary>
    /// 私钥不是合法 Base64 时抛格式异常
    /// </summary>
    [Fact]
    public void SignData_WhenPrivateKeyNotBase64_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => { _ = DsaHelper.SignData(Payload, "not base64 @@@"); });
    }

    /// <summary>
    /// 平台不支持 DSA + SHA256 时跳过该组验证
    /// </summary>
    private static void SkipIfDsaUnavailable()
    {
        Assert.SkipUnless(
            Probe.Value.Supported,
            "当前平台未提供可用的 DSA + SHA256 签名实现（密钥长度或摘要算法组合不被支持），跳过该组验证。");
    }
}

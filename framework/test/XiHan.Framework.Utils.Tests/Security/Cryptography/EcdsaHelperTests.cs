// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// ECDSA 签名辅助类测试
/// </summary>
/// <remarks>
/// ECDSA 只能签名验签、不能加解密。签名带随机 k，同一份数据两次签名必然不同，
/// 所以这里锁的是「验签结果」而不是签名本身；同时锁死曲线为 nistP256——
/// 换曲线会直接改变签名长度与互操作性，属于必须显式确认的变更。
/// </remarks>
public class EcdsaHelperTests
{
    private const string Payload = "曦寒框架·ECDSA 中文报文签名";

    /// <summary>
    /// 整个测试类共享一对密钥，避免每个用例都做一次曲线运算
    /// </summary>
    private static readonly Lazy<(string PublicKey, string PrivateKey)> KeyPair = new(() => EcdsaHelper.GenerateKeys());

    /// <summary>
    /// 另一对不相干的密钥，用于反向用例
    /// </summary>
    private static readonly Lazy<(string PublicKey, string PrivateKey)> OtherKeyPair = new(() => EcdsaHelper.GenerateKeys());

    /// <summary>
    /// 生成的密钥对元组顺序是（公钥，私钥），且公钥是 256 位 SubjectPublicKeyInfo
    /// </summary>
    /// <remarks>
    /// 同目录下的 <see cref="EciesHelper.GenerateKeyPair"/> 返回的是（私钥，公钥），顺序相反，
    /// 调用方极易搞混，因此这里把本类的顺序显式锁死。
    /// </remarks>
    [Fact]
    public void GenerateKeys_ReturnsPublicKeyFirstOnP256()
    {
        var (publicKey, privateKey) = KeyPair.Value;

        Assert.NotEqual(publicKey, privateKey);

        using var fromPublic = ECDsa.Create();
        fromPublic.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        Assert.Equal(256, fromPublic.KeySize);

        using var fromPrivate = ECDsa.Create();
        fromPrivate.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        Assert.Equal(256, fromPrivate.KeySize);
    }

    /// <summary>
    /// 字节形式与字符串形式的密钥对内容一致（只是编码不同）
    /// </summary>
    [Fact]
    public void GenerateKeysBytes_ProducesImportableKeyMaterial()
    {
        var (publicKeyBytes, privateKeyBytes) = EcdsaHelper.GenerateKeysBytes();

        var signature = EcdsaHelper.SignDataBytes(Encoding.UTF8.GetBytes(Payload), privateKeyBytes);

        Assert.True(EcdsaHelper.VerifyDataBytes(
            Encoding.UTF8.GetBytes(Payload),
            Convert.FromBase64String(signature),
            publicKeyBytes));
    }

    /// <summary>
    /// 每次生成的密钥对都不同
    /// </summary>
    [Fact]
    public void GenerateKeys_ProducesDistinctKeyPairs()
    {
        Assert.NotEqual(KeyPair.Value.PublicKey, OtherKeyPair.Value.PublicKey);
    }

    /// <summary>
    /// 签名与验签往返
    /// </summary>
    [Fact]
    public void SignDataVerifyData_RoundTrips()
    {
        var signature = EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.True(EcdsaHelper.VerifyData(Payload, signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 同一份数据两次签名不同，但都能通过验签
    /// </summary>
    [Fact]
    public void SignData_IsNonDeterministicButAlwaysVerifiable()
    {
        var first = EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);
        var second = EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.NotEqual(first, second);
        Assert.True(EcdsaHelper.VerifyData(Payload, first, KeyPair.Value.PublicKey));
        Assert.True(EcdsaHelper.VerifyData(Payload, second, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 空字符串也是可签名的合法数据
    /// </summary>
    [Fact]
    public void SignDataVerifyData_WithEmptyData_RoundTrips()
    {
        var signature = EcdsaHelper.SignData(string.Empty, KeyPair.Value.PrivateKey);

        Assert.True(EcdsaHelper.VerifyData(string.Empty, signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 超长数据签名验签往返
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SignDataVerifyData_WithVeryLongData_RoundTrips()
    {
        var longText = string.Concat(Enumerable.Repeat("曦寒-XiHan-0123456789-", 20_000));

        var signature = EcdsaHelper.SignData(longText, KeyPair.Value.PrivateKey);

        Assert.True(EcdsaHelper.VerifyData(longText, signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 原文被篡改后验签失败
    /// </summary>
    [Fact]
    public void VerifyData_WhenDataTampered_ReturnsFalse()
    {
        var signature = EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.False(EcdsaHelper.VerifyData(Payload + "!", signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 签名被篡改后验签失败
    /// </summary>
    [Fact]
    public void VerifyData_WhenSignatureTampered_ReturnsFalse()
    {
        var signatureBytes = Convert.FromBase64String(EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey));
        signatureBytes[0] ^= 0xFF;

        Assert.False(EcdsaHelper.VerifyDataBytes(
            Encoding.UTF8.GetBytes(Payload),
            signatureBytes,
            Convert.FromBase64String(KeyPair.Value.PublicKey)));
    }

    /// <summary>
    /// 签名长度被截断后验签失败而不是抛异常
    /// </summary>
    [Fact]
    public void VerifyData_WhenSignatureTruncated_ReturnsFalse()
    {
        var signatureBytes = Convert.FromBase64String(EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey));

        Assert.False(EcdsaHelper.VerifyDataBytes(
            Encoding.UTF8.GetBytes(Payload),
            signatureBytes[..(signatureBytes.Length - 1)],
            Convert.FromBase64String(KeyPair.Value.PublicKey)));
    }

    /// <summary>
    /// 用不配对的公钥验签失败
    /// </summary>
    [Fact]
    public void VerifyData_WithUnrelatedPublicKey_ReturnsFalse()
    {
        var signature = EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.False(EcdsaHelper.VerifyData(Payload, signature, OtherKeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 中文报文按 UTF-8 编码后签名，验签时同样按 UTF-8 还原
    /// </summary>
    [Fact]
    public void SignData_ForChinese_UsesUtf8Encoding()
    {
        var signature = EcdsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.True(EcdsaHelper.VerifyDataBytes(
            Encoding.UTF8.GetBytes(Payload),
            Convert.FromBase64String(signature),
            Convert.FromBase64String(KeyPair.Value.PublicKey)));
    }

    /// <summary>
    /// 私钥不是合法 Base64 时抛格式异常
    /// </summary>
    [Fact]
    public void SignData_WhenPrivateKeyNotBase64_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => { _ = EcdsaHelper.SignData(Payload, "not base64 @@@"); });
    }

    /// <summary>
    /// 私钥编码非法时抛加密异常
    /// </summary>
    [Fact]
    public void SignData_WhenPrivateKeyMalformed_ThrowsCryptographicException()
    {
        var garbage = Convert.ToBase64String(new byte[64]);

        Assert.Throws<CryptographicException>(() => { _ = EcdsaHelper.SignData(Payload, garbage); });
    }
}

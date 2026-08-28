// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// ECIES 椭圆曲线混合加密辅助类测试
/// </summary>
/// <remarks>
/// <para>
/// 密文结构是「发送方 SubjectPublicKeyInfo 公钥 + AES IV(16) + AES 密文」三段直接拼接，
/// 没有任何长度前缀，所以解密端必须知道第一段有多长才能切开。
/// </para>
/// <para>
/// 注意 <see cref="EciesHelper.GenerateKeyPair"/> 返回的元组顺序是（私钥，公钥），
/// 与同目录的 <see cref="EcdsaHelper.GenerateKeys"/>（公钥在前）相反，这里显式锁死以防调用方搞混。
/// </para>
/// </remarks>
public class EciesHelperTests
{
    private const string Payload = "曦寒框架·ECIES 中文加密";

    /// <summary>
    /// 生成的密钥对元组顺序是（私钥，公钥）
    /// </summary>
    /// <remarks>
    /// 私钥是 SEC1 <c>ECPrivateKey</c> 编码（<c>ImportECPrivateKey</c> 能读），
    /// 公钥是 X.509 <c>SubjectPublicKeyInfo</c> 编码（<c>ImportSubjectPublicKeyInfo</c> 能读）；
    /// 两者调换会立刻在下面的导入断言上失败。
    /// </remarks>
    [Fact]
    public void GenerateKeyPair_ReturnsPrivateKeyFirstOnP256()
    {
        var (privateKey, publicKey) = EciesHelper.GenerateKeyPair();

        using var fromPrivate = ECDsa.Create();
        fromPrivate.ImportECPrivateKey(Convert.FromBase64String(privateKey), out _);
        Assert.Equal(256, fromPrivate.KeySize);

        using var fromPublic = ECDsa.Create();
        fromPublic.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        Assert.Equal(256, fromPublic.KeySize);
    }

    /// <summary>
    /// 每次生成的密钥对都不同
    /// </summary>
    [Fact]
    public void GenerateKeyPair_ProducesDistinctKeyPairs()
    {
        var first = EciesHelper.GenerateKeyPair();
        var second = EciesHelper.GenerateKeyPair();

        Assert.NotEqual(first.privateKey, second.privateKey);
        Assert.NotEqual(first.publicKey, second.publicKey);
    }

    /// <summary>
    /// 密文由「发送方公钥 + 16 字节 IV + AES 密文块」拼成，长度可精确推算
    /// </summary>
    /// <remarks>
    /// 发送方与接收方用的都是 nistP256，SubjectPublicKeyInfo 编码长度相同，
    /// 因此可以直接拿接收方公钥的长度当作第一段长度来核对整体结构。
    /// </remarks>
    [Fact]
    public void EncryptBytes_LaysOutSenderKeyThenIvThenCipherBlocks()
    {
        var (_, publicKeyBytes) = EciesHelper.GenerateKeyPairBytes();
        var plainBytes = new byte[5];

        var encrypted = EciesHelper.EncryptBytes(publicKeyBytes, plainBytes);

        Assert.Equal(publicKeyBytes.Length + 16 + 16, encrypted.Length);
    }

    /// <summary>
    /// 每次加密都会生成新的临时密钥与 IV，同明文密文必然不同
    /// </summary>
    [Fact]
    public void Encrypt_ForSamePlainText_ProducesDifferentCipherEachTime()
    {
        var (_, publicKey) = EciesHelper.GenerateKeyPair();

        Assert.NotEqual(EciesHelper.Encrypt(publicKey, Payload), EciesHelper.Encrypt(publicKey, Payload));
    }

    /// <summary>
    /// 加解密往返应当还原原文
    /// </summary>
    /// <remarks>
    /// 【已知红灯 / 疑似缺陷】<c>DecryptBytes</c> 用 <c>ECDiffieHellman.Create().KeySize / 8</c>
    /// 推算发送方公钥占用的字节数（默认曲线 521 位 → 65 字节），
    /// 但实际写进密文头部的是 nistP256 的 SubjectPublicKeyInfo（91 字节），两者对不上，
    /// 切片位置全错，导入发送方公钥时必然失败。
    /// 按正确语义断言「往返能还原原文」，缺陷已上报由主控裁决，不迁就现状把断言改成期待异常。
    /// </remarks>
    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        var (privateKey, publicKey) = EciesHelper.GenerateKeyPair();

        var cipher = EciesHelper.Encrypt(publicKey, Payload);

        Assert.Equal(Payload, EciesHelper.Decrypt(privateKey, cipher));
    }

    /// <summary>
    /// 字节形式的加解密往返同样应当还原原文
    /// </summary>
    /// <remarks>与 <see cref="EncryptDecrypt_RoundTrips"/> 同因，见该用例的说明。</remarks>
    [Fact]
    public void EncryptBytesDecryptBytes_RoundTrips()
    {
        var (privateKeyBytes, publicKeyBytes) = EciesHelper.GenerateKeyPairBytes();
        var plainBytes = Encoding.UTF8.GetBytes(Payload);

        var encrypted = EciesHelper.EncryptBytes(publicKeyBytes, plainBytes);

        Assert.Equal(plainBytes, EciesHelper.DecryptBytes(privateKeyBytes, encrypted));
    }

    /// <summary>
    /// 公钥不是合法 Base64 时抛格式异常
    /// </summary>
    [Fact]
    public void Encrypt_WhenPublicKeyNotBase64_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => { _ = EciesHelper.Encrypt("not base64 @@@", Payload); });
    }

    /// <summary>
    /// 公钥编码非法时抛加密异常
    /// </summary>
    [Fact]
    public void Encrypt_WhenPublicKeyMalformed_ThrowsCryptographicException()
    {
        var garbage = Convert.ToBase64String(new byte[64]);

        Assert.Throws<CryptographicException>(() => { _ = EciesHelper.Encrypt(garbage, Payload); });
    }
}

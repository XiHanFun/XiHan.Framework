// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// ECIES 加解密往返回归测试
/// </summary>
/// <remarks>
/// <para>
/// 修复前 <c>DecryptBytes</c> 用 <c>ECDiffieHellman.Create().KeySize / 8</c> 推算密文头部长度，
/// 拿到的是默认曲线（nistP521）的 65 字节；而 <c>EncryptBytes</c> 写进头部的是 nistP256 的
/// SubjectPublicKeyInfo（91 字节），三段切片位置全错，导入发送方公钥必然抛 CryptographicException，
/// 整个类的加解密往返不可用。
/// </para>
/// <para>
/// 密文格式（发送方公钥 + 16 字节 IV + AES 密文，无长度前缀）保持不变，
/// 修复只发生在解密端取头部长度的方式上，因此这里同时钉死密文布局与往返结果。
/// </para>
/// </remarks>
public class EciesHelperRoundTripTests
{
    private const string Payload = "曦寒框架·ECIES 中文加密 payload";

    /// <summary>
    /// 字符串形式的加解密往返还原原文
    /// </summary>
    [Fact]
    public void EncryptDecrypt_RoundTripsPlainText()
    {
        var (privateKey, publicKey) = EciesHelper.GenerateKeyPair();

        var cipher = EciesHelper.Encrypt(publicKey, Payload);

        Assert.Equal(Payload, EciesHelper.Decrypt(privateKey, cipher));
    }

    /// <summary>
    /// 各种长度的明文都能往返还原（含空明文与跨多个 AES 块的长明文）
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(1024)]
    public void EncryptBytesDecryptBytes_RoundTripsAnyLength(int length)
    {
        var (privateKeyBytes, publicKeyBytes) = EciesHelper.GenerateKeyPairBytes();
        var plainBytes = new byte[length];
        RandomNumberGenerator.Fill(plainBytes);

        var encrypted = EciesHelper.EncryptBytes(publicKeyBytes, plainBytes);

        Assert.Equal(plainBytes, EciesHelper.DecryptBytes(privateKeyBytes, encrypted));
    }

    /// <summary>
    /// 同一密钥对反复往返都稳定（每次加密用的临时密钥与 IV 都不同）
    /// </summary>
    [Fact]
    public void EncryptDecrypt_RepeatedRoundTripsAreStable()
    {
        var (privateKey, publicKey) = EciesHelper.GenerateKeyPair();

        for (var i = 0; i < 10; i++)
        {
            var cipher = EciesHelper.Encrypt(publicKey, Payload);
            Assert.Equal(Payload, EciesHelper.Decrypt(privateKey, cipher));
        }
    }

    /// <summary>
    /// 密文头部就是一份完整的发送方 SubjectPublicKeyInfo，长度与接收方公钥编码相同
    /// </summary>
    /// <remarks>
    /// 这条是解密端取头部长度的前提：ECDH 要求收发双方同曲线，同曲线的 SubjectPublicKeyInfo 编码等长。
    /// 前提一旦被破坏（例如改成别的曲线或加了长度前缀），这里会先红，提醒同步修改解密端。
    /// </remarks>
    [Fact]
    public void EncryptBytes_HeaderIsCompleteSenderSubjectPublicKeyInfo()
    {
        var (_, publicKeyBytes) = EciesHelper.GenerateKeyPairBytes();

        var encrypted = EciesHelper.EncryptBytes(publicKeyBytes, [1, 2, 3]);
        var header = encrypted[..publicKeyBytes.Length];

        using var senderPublicKey = ECDiffieHellman.Create();
        senderPublicKey.ImportSubjectPublicKeyInfo(header, out var bytesRead);

        Assert.Equal(publicKeyBytes.Length, bytesRead);
        Assert.Equal(256, senderPublicKey.KeySize);
    }

    /// <summary>
    /// 用另一把私钥解密拿不到原文（要么抛加密异常，要么得到不同的字节）
    /// </summary>
    [Fact]
    public void DecryptBytes_WithWrongPrivateKey_DoesNotRecoverPlainText()
    {
        var (_, publicKeyBytes) = EciesHelper.GenerateKeyPairBytes();
        var (otherPrivateKeyBytes, _) = EciesHelper.GenerateKeyPairBytes();
        var plainBytes = Encoding.UTF8.GetBytes(Payload);

        var encrypted = EciesHelper.EncryptBytes(publicKeyBytes, plainBytes);

        byte[]? decrypted = null;
        var exception = Record.Exception(() => { decrypted = EciesHelper.DecryptBytes(otherPrivateKeyBytes, encrypted); });

        if (exception is null)
        {
            Assert.NotEqual(plainBytes, decrypted);
        }
        else
        {
            Assert.IsAssignableFrom<CryptographicException>(exception);
        }
    }

    /// <summary>
    /// 二进制明文（含 0 字节与高位字节）不会在往返中被编码破坏
    /// </summary>
    [Fact]
    public void EncryptBytesDecryptBytes_RoundTripsBinaryPayload()
    {
        var (privateKeyBytes, publicKeyBytes) = EciesHelper.GenerateKeyPairBytes();
        var plainBytes = new byte[] { 0x00, 0xFF, 0x10, 0x00, 0x7F, 0x80, 0x00 };

        var encrypted = EciesHelper.EncryptBytes(publicKeyBytes, plainBytes);

        Assert.Equal(plainBytes, EciesHelper.DecryptBytes(privateKeyBytes, encrypted));
    }
}

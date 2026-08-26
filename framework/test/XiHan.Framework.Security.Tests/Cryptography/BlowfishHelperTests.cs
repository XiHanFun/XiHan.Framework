// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Security.Cryptography;

namespace XiHan.Framework.Security.Tests.Cryptography;

/// <summary>
/// Blowfish 对称加密辅助类的测试
/// </summary>
/// <remarks>
/// 覆盖 CBC + PKCS7 模式下的加解密往返、随机 IV、错误密钥与非法输入场景。
/// </remarks>
public class BlowfishHelperTests
{
    private const string Key = "blowfish-key-123";
    private const string OtherKey = "other-key-4567890";

    /// <summary>
    /// 加密后使用相同密钥解密应还原原始明文
    /// </summary>
    /// <param name="plaintext">待加密的明文</param>
    [Theory]
    [InlineData("Hello, Blowfish!")]
    [InlineData("")]
    [InlineData("中文 Blowfish 加密数据")]
    public void Encrypt_Decrypt_RoundTrips(string plaintext)
    {
        var ciphertext = BlowfishHelper.Encrypt(plaintext, Key);

        Assert.Equal(plaintext, BlowfishHelper.Decrypt(ciphertext, Key));
    }

    /// <summary>
    /// 相同明文与密钥多次加密应产生不同密文（随机 IV）
    /// </summary>
    [Fact]
    public void Encrypt_SamePlaintextAndKey_ProducesDifferentCiphertexts()
    {
        const string plaintext = "identical plaintext";

        var first = BlowfishHelper.Encrypt(plaintext, Key);
        var second = BlowfishHelper.Encrypt(plaintext, Key);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 使用不同密钥解密不应还原出原始明文
    /// </summary>
    [Fact]
    public void Decrypt_WithDifferentKey_DoesNotRecoverPlaintext()
    {
        const string plaintext = "Blowfish secret data";
        var ciphertext = BlowfishHelper.Encrypt(plaintext, Key);

        string? decrypted = null;
        var exception = Record.Exception(() => decrypted = BlowfishHelper.Decrypt(ciphertext, OtherKey));

        Assert.True(exception is not null || decrypted != plaintext);
    }

    /// <summary>
    /// 密钥超过 56 字节时应抛出异常
    /// </summary>
    [Fact]
    public void Encrypt_KeyTooLong_Throws()
    {
        var tooLongKey = new string('k', 57);

        Assert.Throws<ArgumentException>(() => BlowfishHelper.Encrypt("data", tooLongKey));
    }

    /// <summary>
    /// 密文长度不足（缺少 8 字节 IV）时应抛出异常
    /// </summary>
    [Fact]
    public void DecryptBytes_TooShortCiphertext_Throws()
    {
        var key = Encoding.UTF8.GetBytes(Key);

        Assert.Throws<ArgumentException>(() => BlowfishHelper.DecryptBytes(new byte[4], key));
    }
}

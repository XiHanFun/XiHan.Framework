// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Security.Cryptography;

namespace XiHan.Framework.Security.Tests.Cryptography;

/// <summary>
/// SM4 对称加密辅助类的测试
/// </summary>
/// <remarks>
/// 覆盖 CBC + PKCS7 模式下字符串与字节级别的加解密往返、非法密钥长度、非法密文与错误密钥场景。
/// </remarks>
public class Sm4HelperTests
{
    private const string FirstKey = "1234567890123456";
    private const string SecondKey = "abcdefghijklmnop";

    /// <summary>
    /// 加密后使用相同密钥解密应还原原始明文
    /// </summary>
    /// <param name="plaintext">待加密的明文</param>
    [Theory]
    [InlineData("Hello, SM4!")]
    [InlineData("")]
    [InlineData("国密 SM4 算法测试数据")]
    [InlineData("mixed 123 !@# Chinese 中文")]
    public void Encrypt_Decrypt_RoundTrips(string plaintext)
    {
        var ciphertext = Sm4Helper.Encrypt(plaintext, FirstKey);

        Assert.Equal(plaintext, Sm4Helper.Decrypt(ciphertext, FirstKey));
    }

    /// <summary>
    /// 字节级加密后使用相同密钥解密应还原原始字节数据
    /// </summary>
    [Fact]
    public void EncryptBytes_DecryptBytes_RoundTrip()
    {
        var plaintext = new byte[] { 0x00, 0x01, 0x02, 0x7F, 0x80, 0xFE, 0xFF, 0x41, 0x00, 0x1B };
        var key = Encoding.UTF8.GetBytes(FirstKey);

        var ciphertext = Sm4Helper.EncryptBytes(plaintext, key);

        Assert.Equal(plaintext, Sm4Helper.DecryptBytes(ciphertext, key));
    }

    /// <summary>
    /// 密钥长度不是 16 字节时应抛出异常
    /// </summary>
    /// <param name="key">长度非法的密钥</param>
    [Theory]
    [InlineData("short")]
    [InlineData("")]
    [InlineData("this-key-is-way-too-long-to-be-valid")]
    public void Encrypt_InvalidKeyLength_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => Sm4Helper.Encrypt("data", key));
    }

    /// <summary>
    /// 密文长度不足（缺少 16 字节 IV）时应抛出异常
    /// </summary>
    [Fact]
    public void DecryptBytes_TooShortCiphertext_Throws()
    {
        var key = Encoding.UTF8.GetBytes(FirstKey);

        Assert.Throws<ArgumentException>(() => Sm4Helper.DecryptBytes(new byte[8], key));
    }

    /// <summary>
    /// 传入非法 Base64 密文时应抛出异常
    /// </summary>
    [Fact]
    public void Decrypt_InvalidBase64_Throws()
    {
        Assert.Throws<FormatException>(() => Sm4Helper.Decrypt("***invalid-base64***", FirstKey));
    }

    /// <summary>
    /// 使用不同密钥解密不应还原出原始明文
    /// </summary>
    [Fact]
    public void Decrypt_WithDifferentKey_DoesNotRecoverPlaintext()
    {
        const string plaintext = "国密 SM4 秘密数据";
        var ciphertext = Sm4Helper.Encrypt(plaintext, FirstKey);

        string? decrypted = null;
        var exception = Record.Exception(() => decrypted = Sm4Helper.Decrypt(ciphertext, SecondKey));

        Assert.True(exception is not null || decrypted != plaintext);
    }
}

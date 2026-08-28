// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// AES 加解密辅助类测试
/// </summary>
/// <remarks>
/// 单口令重载用全零盐从口令 PBKDF2 派生 Key 与 IV，因此是确定性的——这一点被文档
/// （docs/guide/security.md）明确记录为已知行为，此处用「同明文同口令必得同密文」把它锁死：
/// 一旦有人改成随机盐/随机 IV 而没同步改文档与调用方，用例会立刻变红。
/// 三参重载则要求调用方自己保证 Key 为 16/24/32 字节、IV 为 16 字节。
/// </remarks>
public class AesHelperTests
{
    private const string Password = "XiHan-Test-Password";
    private const string Key256 = "0123456789abcdef0123456789abcdef";
    private const string Iv128 = "0123456789abcdef";
    private const string ChineseSample = "曦寒框架·中文加密测试，含标点与符号 #￥%……";

    /// <summary>
    /// 口令重载的加解密往返
    /// </summary>
    [Fact]
    public void EncryptDecrypt_WithPassword_RoundTrips()
    {
        var cipher = AesHelper.Encrypt("plain text", Password);

        Assert.NotEqual("plain text", cipher);
        Assert.Equal("plain text", AesHelper.Decrypt(cipher, Password));
    }

    /// <summary>
    /// 口令重载对同一明文恒定产出同一密文
    /// </summary>
    [Fact]
    public void Encrypt_WithSamePassword_IsDeterministic()
    {
        Assert.Equal(AesHelper.Encrypt("plain text", Password), AesHelper.Encrypt("plain text", Password));
    }

    /// <summary>
    /// 不同口令产出不同密文
    /// </summary>
    [Fact]
    public void Encrypt_WithDifferentPassword_ProducesDifferentCipher()
    {
        Assert.NotEqual(AesHelper.Encrypt("plain text", "password-a"), AesHelper.Encrypt("plain text", "password-b"));
    }

    /// <summary>
    /// 用错误口令解密拿不回原文
    /// </summary>
    /// <remarks>
    /// CBC 模式下错误密钥通常在去填充阶段抛 <see cref="CryptographicException"/>，
    /// 但也存在小概率填充恰好合法的情况，所以断言写成「要么抛异常、要么解不出原文」，
    /// 而不是硬性要求必抛——后者会把测试变成对具体填充结果的赌博。
    /// </remarks>
    [Fact]
    public void Decrypt_WithWrongPassword_DoesNotRecoverPlainText()
    {
        var cipher = AesHelper.Encrypt("plain text", Password);

        string? decrypted = null;
        try
        {
            decrypted = AesHelper.Decrypt(cipher, "wrong-password");
        }
        catch (CryptographicException)
        {
            // 预期路径：填充校验失败
        }

        Assert.NotEqual("plain text", decrypted);
    }

    /// <summary>
    /// 密文不是合法 Base64 时抛格式异常
    /// </summary>
    [Fact]
    public void Decrypt_WhenCipherTextNotBase64_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => { _ = AesHelper.Decrypt("not base64 @@@", Password); });
    }

    /// <summary>
    /// 自定义 Key/IV 重载的加解密往返
    /// </summary>
    [Fact]
    public void EncryptDecrypt_WithCustomKeyAndIv_RoundTrips()
    {
        var cipher = AesHelper.Encrypt("plain text", Key256, Iv128);

        Assert.Equal("plain text", AesHelper.Decrypt(cipher, Key256, Iv128));
    }

    /// <summary>
    /// 换 IV 会得到不同密文，但用同一 IV 仍能解回
    /// </summary>
    [Fact]
    public void Encrypt_WithDifferentIv_ProducesDifferentCipher()
    {
        const string OtherIv = "fedcba9876543210";

        var cipher1 = AesHelper.Encrypt("plain text", Key256, Iv128);
        var cipher2 = AesHelper.Encrypt("plain text", Key256, OtherIv);

        Assert.NotEqual(cipher1, cipher2);
        Assert.Equal("plain text", AesHelper.Decrypt(cipher2, Key256, OtherIv));
    }

    /// <summary>
    /// 16/24/32 字节的 Key 都被接受
    /// </summary>
    [Theory]
    [InlineData("0123456789abcdef")]
    [InlineData("0123456789abcdef01234567")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void EncryptDecrypt_WithLegalKeySizes_RoundTrips(string key)
    {
        var cipher = AesHelper.Encrypt(ChineseSample, key, Iv128);

        Assert.Equal(ChineseSample, AesHelper.Decrypt(cipher, key, Iv128));
    }

    /// <summary>
    /// 非法 Key 长度抛加密异常
    /// </summary>
    [Theory]
    [InlineData("short")]
    [InlineData("0123456789abcde")]
    [InlineData("0123456789abcdef0")]
    public void Encrypt_WithIllegalKeySize_ThrowsCryptographicException(string key)
    {
        Assert.Throws<CryptographicException>(() => { _ = AesHelper.Encrypt("plain text", key, Iv128); });
    }

    /// <summary>
    /// 非法 IV 长度抛加密异常
    /// </summary>
    [Fact]
    public void Encrypt_WithIllegalIvSize_ThrowsCryptographicException()
    {
        Assert.Throws<CryptographicException>(() => { _ = AesHelper.Encrypt("plain text", Key256, "too-short-iv"); });
    }

    /// <summary>
    /// 中文明文按 UTF-8 往返后不乱码
    /// </summary>
    [Fact]
    public void EncryptDecrypt_WithChinese_RoundTripsWithoutMojibake()
    {
        var cipher = AesHelper.Encrypt(ChineseSample, Password);

        Assert.Equal(ChineseSample, AesHelper.Decrypt(cipher, Password));
    }

    /// <summary>
    /// 空明文加密后仍是一个完整填充块，解密回空串
    /// </summary>
    [Fact]
    public void EncryptDecrypt_WithEmptyPlainText_RoundTripsToEmpty()
    {
        var cipher = AesHelper.Encrypt(string.Empty, Password);

        Assert.NotEqual(string.Empty, cipher);
        Assert.Equal(16, Convert.FromBase64String(cipher).Length);
        Assert.Equal(string.Empty, AesHelper.Decrypt(cipher, Password));
    }

    /// <summary>
    /// 超长明文往返不丢字节
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EncryptDecrypt_WithVeryLongPlainText_RoundTrips()
    {
        var longText = string.Concat(Enumerable.Repeat("曦寒-XiHan-0123456789-", 20_000));

        var cipher = AesHelper.Encrypt(longText, Password);

        Assert.Equal(longText, AesHelper.Decrypt(cipher, Password));
    }

    /// <summary>
    /// 密文长度始终是 16 字节块的整数倍且严格大于明文长度（PKCS7 填充）
    /// </summary>
    [Theory]
    [InlineData(0, 16)]
    [InlineData(1, 16)]
    [InlineData(15, 16)]
    [InlineData(16, 32)]
    [InlineData(17, 32)]
    public void EncryptBytes_PadsToBlockBoundary(int plainLength, int expectedCipherLength)
    {
        var plainBytes = new byte[plainLength];
        var keyBytes = Encoding.UTF8.GetBytes(Key256);
        var ivBytes = Encoding.UTF8.GetBytes(Iv128);

        var cipherBytes = AesHelper.EncryptBytes(plainBytes, keyBytes, ivBytes);

        Assert.Equal(expectedCipherLength, cipherBytes.Length);
        Assert.Equal(plainBytes, AesHelper.DecryptBytes(cipherBytes, keyBytes, ivBytes));
    }

    /// <summary>
    /// 字节重载与字符串重载对同一份数据结果一致
    /// </summary>
    [Fact]
    public void EncryptBytes_MatchesStringOverload()
    {
        var keyBytes = Encoding.UTF8.GetBytes(Key256);
        var ivBytes = Encoding.UTF8.GetBytes(Iv128);

        var expected = Convert.ToBase64String(
            AesHelper.EncryptBytes(Encoding.UTF8.GetBytes(ChineseSample), keyBytes, ivBytes));

        Assert.Equal(expected, AesHelper.Encrypt(ChineseSample, Key256, Iv128));
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// DES 加解密辅助类测试
/// </summary>
/// <remarks>
/// DES 只保留用于对接遗留系统，单参重载用的是写死在代码里的密钥与 IV，因此是确定性的——
/// 这是文档明确记载的行为，这里把它锁死，避免有人以为它安全而改成随机 IV 后打断存量数据的解密。
/// OpenSSL 3 把单 DES 移到了 legacy provider，部分平台可能整个算法不可用，
/// 所以先探测一次，不可用就整体跳过而不是误报失败。
/// </remarks>
public class DesHelperTests
{
    private const string ChineseSample = "曦寒框架·DES 中文往返";

    /// <summary>
    /// 平台是否提供 DES 实现
    /// </summary>
    private static readonly Lazy<bool> DesAvailable = new(() =>
    {
        try
        {
            _ = DesHelper.Encrypt("probe");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    });

    /// <summary>
    /// 默认密钥的加解密往返
    /// </summary>
    [Fact]
    public void EncryptDecrypt_WithDefaultKey_RoundTrips()
    {
        SkipIfDesUnavailable();

        var cipher = DesHelper.Encrypt("plain text");

        Assert.NotEqual("plain text", cipher);
        Assert.Equal("plain text", DesHelper.Decrypt(cipher));
    }

    /// <summary>
    /// 默认密钥重载对同一明文恒定产出同一密文
    /// </summary>
    [Fact]
    public void Encrypt_WithDefaultKey_IsDeterministic()
    {
        SkipIfDesUnavailable();

        Assert.Equal(DesHelper.Encrypt("plain text"), DesHelper.Encrypt("plain text"));
    }

    /// <summary>
    /// 中文明文按 UTF-8 往返后不乱码
    /// </summary>
    [Fact]
    public void EncryptDecrypt_WithChinese_RoundTripsWithoutMojibake()
    {
        SkipIfDesUnavailable();

        var cipher = DesHelper.Encrypt(ChineseSample);

        Assert.Equal(ChineseSample, DesHelper.Decrypt(cipher));
    }

    /// <summary>
    /// 空明文加密后是一个完整填充块，解密回空串
    /// </summary>
    [Fact]
    public void EncryptDecrypt_WithEmptyPlainText_RoundTripsToEmpty()
    {
        SkipIfDesUnavailable();

        var cipher = DesHelper.Encrypt(string.Empty);

        Assert.Equal(8, Convert.FromBase64String(cipher).Length);
        Assert.Equal(string.Empty, DesHelper.Decrypt(cipher));
    }

    /// <summary>
    /// 自定义 8 字节密钥与 IV 的加解密往返
    /// </summary>
    [Fact]
    public void EncryptBytesDecryptBytes_WithCustomKeyAndIv_RoundTrips()
    {
        SkipIfDesUnavailable();

        var keyBytes = Encoding.UTF8.GetBytes("ABCDEFGH");
        var ivBytes = Encoding.UTF8.GetBytes("HGFEDCBA");
        var plainBytes = Encoding.UTF8.GetBytes(ChineseSample);

        var cipher = DesHelper.EncryptBytes(plainBytes, keyBytes, ivBytes);

        Assert.Equal(ChineseSample, DesHelper.DecryptBytes(Convert.FromBase64String(cipher), keyBytes, ivBytes));
    }

    /// <summary>
    /// 用错误密钥解密拿不回原文
    /// </summary>
    /// <remarks>
    /// 与 AES 同理：错误密钥通常在去填充阶段抛异常，但存在填充恰好合法的小概率，
    /// 所以断言写成「要么抛异常、要么解不出原文」。
    /// </remarks>
    [Fact]
    public void DecryptBytes_WithWrongKey_DoesNotRecoverPlainText()
    {
        SkipIfDesUnavailable();

        var keyBytes = Encoding.UTF8.GetBytes("ABCDEFGH");
        var wrongKeyBytes = Encoding.UTF8.GetBytes("HGFEDCBA");
        var ivBytes = Encoding.UTF8.GetBytes("12345678");

        var cipher = Convert.FromBase64String(
            DesHelper.EncryptBytes(Encoding.UTF8.GetBytes("plain text"), keyBytes, ivBytes));

        string? decrypted = null;
        try
        {
            decrypted = DesHelper.DecryptBytes(cipher, wrongKeyBytes, ivBytes);
        }
        catch (CryptographicException)
        {
            // 预期路径：填充校验失败
        }

        Assert.NotEqual("plain text", decrypted);
    }

    /// <summary>
    /// 非 8 字节密钥抛参数异常
    /// </summary>
    /// <remarks>
    /// 原断言按 <see cref="AesHelper"/> 的对称直觉写成 <see cref="CryptographicException"/>，但两者并不对称：
    /// <see cref="Aes"/> 走 <see cref="SymmetricAlgorithm"/> 基类的 Key setter，非法长度抛 CryptographicException；
    /// <see cref="DES"/> 重写了 Key setter，非法长度先抛 <see cref="ArgumentException"/>（"Specified key is not a valid size for this algorithm."），
    /// 只有弱密钥/半弱密钥才抛 CryptographicException。DesHelper 与 AesHelper 一样是不做长度校验的薄封装，
    /// 直接透传 BCL 异常，因此这里锁的是 BCL 对 DES 的既有口径。
    /// </remarks>
    [Theory]
    [InlineData("short")]
    [InlineData("TOOLONGKEY")]
    public void EncryptBytes_WithIllegalKeySize_ThrowsArgumentException(string key)
    {
        SkipIfDesUnavailable();

        var ivBytes = Encoding.UTF8.GetBytes("12345678");

        Assert.Throws<ArgumentException>(
            () => { _ = DesHelper.EncryptBytes(Encoding.UTF8.GetBytes("plain text"), Encoding.UTF8.GetBytes(key), ivBytes); });
    }

    /// <summary>
    /// 密文长度始终是 8 字节块的整数倍
    /// </summary>
    [Theory]
    [InlineData(0, 8)]
    [InlineData(7, 8)]
    [InlineData(8, 16)]
    public void EncryptBytes_PadsToBlockBoundary(int plainLength, int expectedCipherLength)
    {
        SkipIfDesUnavailable();

        var keyBytes = Encoding.UTF8.GetBytes("ABCDEFGH");
        var ivBytes = Encoding.UTF8.GetBytes("12345678");

        var cipher = Convert.FromBase64String(
            DesHelper.EncryptBytes(new byte[plainLength], keyBytes, ivBytes));

        Assert.Equal(expectedCipherLength, cipher.Length);
    }

    /// <summary>
    /// 平台不提供 DES 时跳过该组验证
    /// </summary>
    private static void SkipIfDesUnavailable()
    {
        Assert.SkipUnless(DesAvailable.Value, "当前平台不提供 DES 实现（OpenSSL 3 legacy provider 未启用），跳过该组验证。");
    }
}

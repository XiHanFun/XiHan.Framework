// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Tests.Security.Cryptography;

/// <summary>
/// RSA 加解密与签名辅助类测试
/// </summary>
/// <remarks>
/// <para>
/// 关键前提：<c>RsaHelper</c> 内部一律用 <see cref="RSA.ImportSubjectPublicKeyInfo"/> 导入公钥，
/// 而 <c>GenerateKeys()</c> 导出的是 PKCS#1 <c>RSAPublicKey</c>——两者不通用。
/// docs/guide/security.md 已把这条限制写进「公钥必须是 SubjectPublicKeyInfo 格式」的警示块，
/// 因此这里把它当作**已记录的契约**锁死（喂 PKCS#1 公钥必须失败），
/// 正向用例则按文档给出的做法自己导出 SPKI 公钥 + PKCS#8 私钥。
/// </para>
/// <para>
/// 私钥没有这个问题：导入时先试 PKCS#8、失败再退回 PKCS#1，两种编码都能吃。
/// </para>
/// <para>2048 位密钥生成较慢，用静态 <see cref="Lazy{T}"/> 在整个测试类内共享一份。</para>
/// </remarks>
public class RsaHelperTests : IDisposable
{
    private const string Payload = "曦寒框架·RSA 报文签名与加密测试 payload";

    /// <summary>
    /// 按文档推荐方式导出的密钥对：公钥 SubjectPublicKeyInfo、私钥 PKCS#8
    /// </summary>
    private static readonly Lazy<(string PublicKey, string PrivateKey)> KeyPair = new(CreateSpkiKeyPair);

    /// <summary>
    /// 另一把不相干的密钥对，用于反向用例
    /// </summary>
    private static readonly Lazy<(string PublicKey, string PrivateKey)> OtherKeyPair = new(CreateSpkiKeyPair);

    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 构造函数，准备 PEM 文件读写用的临时目录
    /// </summary>
    public RsaHelperTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// 密钥长度低于 2048 位时拒绝生成
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2040)]
    public void GenerateKeys_WhenKeySizeBelowMinimum_ThrowsArgumentException(int keySize)
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = RsaHelper.GenerateKeys(keySize); });

        Assert.Contains("2048", exception.Message);
    }

    /// <summary>
    /// 密钥长度不是 8 的倍数时拒绝生成
    /// </summary>
    [Fact]
    public void GenerateKeys_WhenKeySizeNotMultipleOfEight_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => { _ = RsaHelper.GenerateKeys(2049); });

        Assert.Contains("8 的倍数", exception.Message);
    }

    /// <summary>
    /// 字节形式的密钥生成同样受长度校验约束
    /// </summary>
    [Fact]
    public void GenerateKeysBytes_WhenKeySizeBelowMinimum_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => { _ = RsaHelper.GenerateKeysBytes(1024); });
    }

    /// <summary>
    /// 默认生成 2048 位密钥，且导出的是 PKCS#1 编码
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GenerateKeysBytes_ByDefault_Exports2048BitPkcs1Keys()
    {
        var (publicKeyBytes, privateKeyBytes) = RsaHelper.GenerateKeysBytes();

        using var fromPublic = RSA.Create();
        fromPublic.ImportRSAPublicKey(publicKeyBytes, out _);
        Assert.Equal(2048, fromPublic.KeySize);

        using var fromPrivate = RSA.Create();
        fromPrivate.ImportRSAPrivateKey(privateKeyBytes, out _);
        Assert.Equal(2048, fromPrivate.KeySize);
    }

    /// <summary>
    /// 把 GenerateKeys 导出的 PKCS#1 公钥直接喂给 Encrypt 会失败
    /// </summary>
    /// <remarks>
    /// 这是 docs/guide/security.md 明确记录的限制，不是意外行为，因此按「必须失败」锁死；
    /// 一旦哪天内部改成兼容 PKCS#1，需要同步更新文档并改掉这条用例。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void Encrypt_WithPkcs1PublicKey_ThrowsCryptographicException()
    {
        var (publicKey, _) = RsaHelper.GenerateKeys();

        Assert.Throws<CryptographicException>(() => { _ = RsaHelper.Encrypt(Payload, publicKey); });
    }

    /// <summary>
    /// 用 PKCS#1 公钥验签恒为 false（异常被内部吞掉转成 false）
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyData_WithPkcs1PublicKey_ReturnsFalse()
    {
        var (publicKey, privateKey) = RsaHelper.GenerateKeys();
        var signature = RsaHelper.SignData(Payload, privateKey);

        Assert.False(RsaHelper.VerifyData(Payload, signature, publicKey));
    }

    /// <summary>
    /// SubjectPublicKeyInfo 公钥的加解密往返
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EncryptDecrypt_WithSpkiPublicKey_RoundTrips()
    {
        var cipher = RsaHelper.Encrypt(Payload, KeyPair.Value.PublicKey);

        Assert.NotEqual(Payload, cipher);
        Assert.Equal(Payload, RsaHelper.Decrypt(cipher, KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// 明文超过单块上限时自动分段，密文长度是块长的整数倍
    /// </summary>
    /// <remarks>
    /// 2048 位密钥 + OAEP-SHA256 的单块明文上限是 256 - 66 = 190 字节；
    /// 500 字节明文应当被切成 3 块，密文 3 × 256 = 768 字节。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void EncryptDecrypt_WithMultiBlockPlainText_RoundTrips()
    {
        var longText = new string('a', 500);

        var cipherBytes = Convert.FromBase64String(RsaHelper.Encrypt(longText, KeyPair.Value.PublicKey));

        Assert.Equal(768, cipherBytes.Length);
        Assert.Equal(longText, RsaHelper.Decrypt(Convert.ToBase64String(cipherBytes), KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// 中文明文往返后不乱码
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EncryptDecrypt_WithChinese_RoundTripsWithoutMojibake()
    {
        var cipher = RsaHelper.Encrypt(Payload, KeyPair.Value.PublicKey);

        Assert.Equal(Payload, RsaHelper.Decrypt(cipher, KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// 空明文加密后密文为空字节，解密回空串
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EncryptDecrypt_WithEmptyPlainText_RoundTripsToEmpty()
    {
        var cipher = RsaHelper.Encrypt(string.Empty, KeyPair.Value.PublicKey);

        Assert.Equal(string.Empty, cipher);
        Assert.Equal(string.Empty, RsaHelper.Decrypt(cipher, KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// PKCS#1 填充加密的密文必须用同一填充模式解密
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Decrypt_WithMismatchedPadding_ThrowsCryptographicException()
    {
        var cipher = RsaHelper.Encrypt(Payload, KeyPair.Value.PublicKey, RSAEncryptionPadding.Pkcs1);

        Assert.Equal(Payload, RsaHelper.Decrypt(cipher, KeyPair.Value.PrivateKey, RSAEncryptionPadding.Pkcs1));
        Assert.Throws<CryptographicException>(() => { _ = RsaHelper.Decrypt(cipher, KeyPair.Value.PrivateKey); });
    }

    /// <summary>
    /// 用不配对的私钥解密失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Decrypt_WithUnrelatedPrivateKey_ThrowsCryptographicException()
    {
        var cipher = RsaHelper.Encrypt(Payload, KeyPair.Value.PublicKey);

        Assert.Throws<CryptographicException>(() => { _ = RsaHelper.Decrypt(cipher, OtherKeyPair.Value.PrivateKey); });
    }

    /// <summary>
    /// 密文长度不是块长整数倍时拒绝解密
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Decrypt_WhenCipherLengthNotBlockAligned_ThrowsCryptographicException()
    {
        var cipher = Convert.ToBase64String(new byte[100]);

        Assert.Throws<CryptographicException>(() => { _ = RsaHelper.Decrypt(cipher, KeyPair.Value.PrivateKey); });
    }

    /// <summary>
    /// 密文不是合法 Base64 时抛格式异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Decrypt_WhenCipherTextNotBase64_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => { _ = RsaHelper.Decrypt("not base64 @@@", KeyPair.Value.PrivateKey); });
    }

    /// <summary>
    /// 空引用入参抛空引用参数异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Encrypt_WhenArgumentNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = RsaHelper.Encrypt(null!, KeyPair.Value.PublicKey); });
        Assert.Throws<ArgumentNullException>(() => { _ = RsaHelper.Encrypt(Payload, null!); });
    }

    /// <summary>
    /// 混合加密的密文以版本号 1 开头，并能解回原文
    /// </summary>
    /// <remarks>
    /// 版本号是这段自定义封包格式的唯一兼容性抓手，必须锁死；
    /// 结构为 [版本 1 字节][RSA(AESKey) 长度 2 字节][RSA(AESKey)][RSA(AESIV) 长度 2 字节][RSA(AESIV)][AES(Data)]。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public void EncryptWithAes_ProducesVersionedEnvelopeAndRoundTrips()
    {
        var cipherBytes = RsaHelper.EncryptBytesWithAes(Encoding.UTF8.GetBytes(Payload), KeyPair.Value.PublicKey);

        Assert.Equal((byte)1, cipherBytes[0]);
        Assert.Equal(
            Payload,
            Encoding.UTF8.GetString(RsaHelper.DecryptBytesWithAes(cipherBytes, KeyPair.Value.PrivateKey)));
    }

    /// <summary>
    /// 混合加密的字符串重载往返
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EncryptWithAesDecryptWithAes_RoundTrips()
    {
        var cipher = RsaHelper.EncryptWithAes(Payload, KeyPair.Value.PublicKey);

        Assert.Equal(Payload, RsaHelper.DecryptWithAes(cipher, KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// 混合加密每次使用新的 AES 密钥与 IV，因此同明文密文不同
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EncryptWithAes_ForSamePlainText_ProducesDifferentCipherEachTime()
    {
        Assert.NotEqual(
            RsaHelper.EncryptWithAes(Payload, KeyPair.Value.PublicKey),
            RsaHelper.EncryptWithAes(Payload, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 混合加密适合大文本，往返后完全一致
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EncryptWithAes_ForVeryLongPlainText_RoundTrips()
    {
        var longText = string.Concat(Enumerable.Repeat("曦寒-XiHan-0123456789-", 10_000));

        var cipher = RsaHelper.EncryptWithAes(longText, KeyPair.Value.PublicKey);

        Assert.Equal(longText, RsaHelper.DecryptWithAes(cipher, KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// 版本号不为 1 的混合密文被拒绝
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void DecryptBytesWithAes_WhenVersionUnsupported_ThrowsCryptographicException()
    {
        var cipherBytes = RsaHelper.EncryptBytesWithAes(Encoding.UTF8.GetBytes(Payload), KeyPair.Value.PublicKey);
        cipherBytes[0] = 2;

        Assert.Throws<CryptographicException>(() => { _ = RsaHelper.DecryptBytesWithAes(cipherBytes, KeyPair.Value.PrivateKey); });
    }

    /// <summary>
    /// 签名与验签往返
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SignDataVerifyData_RoundTrips()
    {
        var signature = RsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.True(RsaHelper.VerifyData(Payload, signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 原文被篡改后验签失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyData_WhenDataTampered_ReturnsFalse()
    {
        var signature = RsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.False(RsaHelper.VerifyData(Payload + "!", signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 签名被篡改后验签失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyData_WhenSignatureTampered_ReturnsFalse()
    {
        var signatureBytes = RsaHelper.SignDataBytes(Encoding.UTF8.GetBytes(Payload), KeyPair.Value.PrivateKey);
        signatureBytes[0] ^= 0xFF;

        Assert.False(RsaHelper.VerifyDataBytes(Encoding.UTF8.GetBytes(Payload), signatureBytes, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 用不配对的公钥验签失败
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyData_WithUnrelatedPublicKey_ReturnsFalse()
    {
        var signature = RsaHelper.SignData(Payload, KeyPair.Value.PrivateKey);

        Assert.False(RsaHelper.VerifyData(Payload, signature, OtherKeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 签名不是合法 Base64 时验签返回 false 而不是抛异常
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyData_WhenSignatureNotBase64_ReturnsFalse()
    {
        Assert.False(RsaHelper.VerifyData(Payload, "not base64 @@@", KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 换哈希算法后签名不同，且必须用同一算法验签
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SignData_WithDifferentHashAlgorithm_RequiresMatchingVerify()
    {
        var sha512Signature = RsaHelper.SignData(Payload, KeyPair.Value.PrivateKey, HashAlgorithmName.SHA512);

        Assert.NotEqual(RsaHelper.SignData(Payload, KeyPair.Value.PrivateKey), sha512Signature);
        Assert.True(RsaHelper.VerifyData(Payload, sha512Signature, KeyPair.Value.PublicKey, HashAlgorithmName.SHA512));
        Assert.False(RsaHelper.VerifyData(Payload, sha512Signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// PSS 填充的签名不能用默认 PKCS#1 填充验签
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void SignData_WithPssPadding_RequiresMatchingVerifyPadding()
    {
        var pssSignature = RsaHelper.SignData(Payload, KeyPair.Value.PrivateKey, null, RSASignaturePadding.Pss);

        Assert.True(RsaHelper.VerifyData(Payload, pssSignature, KeyPair.Value.PublicKey, null, RSASignaturePadding.Pss));
        Assert.False(RsaHelper.VerifyData(Payload, pssSignature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 配对的公私钥通过配对校验，不配对的不通过
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void VerifyKeyPair_DistinguishesMatchingPair()
    {
        Assert.True(RsaHelper.VerifyKeyPair(KeyPair.Value.PublicKey, KeyPair.Value.PrivateKey));
        Assert.False(RsaHelper.VerifyKeyPair(OtherKeyPair.Value.PublicKey, KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// 公钥与私钥都能读出 2048 位长度
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GetKeySize_ForBothKeyForms_Returns2048()
    {
        Assert.Equal(2048, RsaHelper.GetKeySize(KeyPair.Value.PublicKey));
        Assert.Equal(2048, RsaHelper.GetKeySize(KeyPair.Value.PrivateKey));
    }

    /// <summary>
    /// 密钥为空引用时抛空引用参数异常
    /// </summary>
    [Fact]
    public void GetKeySize_WhenKeyNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = RsaHelper.GetKeySize(null!); });
    }

    /// <summary>
    /// 从私钥导出的公钥是 PKCS#1 编码，能被 ImportRSAPublicKey 读回
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void ExportPublicKeyFromPrivateKey_ProducesPkcs1PublicKey()
    {
        var exported = RsaHelper.ExportPublicKeyFromPrivateKey(KeyPair.Value.PrivateKey);

        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(exported), out _);

        Assert.Equal(2048, rsa.KeySize);
    }

    /// <summary>
    /// PEM 导出按 64 字符换行，且能原样导入回来
    /// </summary>
    [Fact]
    public void ExportToPemImportFromPem_RoundTrips()
    {
        var base64Key = new string('A', 130);

        var pem = RsaHelper.ExportToPem(base64Key, "PUBLIC KEY");

        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", pem);
        Assert.Contains("-----END PUBLIC KEY-----", pem);

        var bodyLines = pem
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim('\r'))
            .Where(line => !line.StartsWith("-----", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(new[] { 64, 64, 2 }, bodyLines.Select(line => line.Length).ToArray());
        Assert.Equal(base64Key, RsaHelper.ImportFromPem(pem, "PUBLIC KEY"));
    }

    /// <summary>
    /// PEM 缺少标记时抛格式异常
    /// </summary>
    [Fact]
    public void ImportFromPem_WhenMarkerMissing_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => { _ = RsaHelper.ImportFromPem("just some text", "PUBLIC KEY"); });
    }

    /// <summary>
    /// PEM 内容为空时抛格式异常
    /// </summary>
    [Fact]
    public void ImportFromPem_WhenBodyEmpty_ThrowsFormatException()
    {
        var pem = "-----BEGIN PUBLIC KEY-----\n-----END PUBLIC KEY-----\n";

        Assert.Throws<FormatException>(() => { _ = RsaHelper.ImportFromPem(pem, "PUBLIC KEY"); });
    }

    /// <summary>
    /// PEM 格式的密钥对同时带公钥与私钥标记
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GenerateKeysPem_ProducesBothPemBlocks()
    {
        var (publicKeyPem, privateKeyPem) = RsaHelper.GenerateKeysPem();

        Assert.Contains("-----BEGIN PUBLIC KEY-----", publicKeyPem);
        Assert.Contains("-----BEGIN PRIVATE KEY-----", privateKeyPem);
    }

    /// <summary>
    /// PEM 文件写入后能原样读回
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void WriteAndReadPemFiles_RoundTrip()
    {
        var publicKeyPath = Path.Combine(_tempDirectory, "public.pem");
        var privateKeyPath = Path.Combine(_tempDirectory, "private.pem");

        RsaHelper.WritePublicKeyToPemFile(KeyPair.Value.PublicKey, publicKeyPath);
        RsaHelper.WritePrivateKeyToPemFile(KeyPair.Value.PrivateKey, privateKeyPath);

        Assert.Equal(KeyPair.Value.PublicKey, RsaHelper.ReadPublicKeyFromPemFile(publicKeyPath));
        Assert.Equal(KeyPair.Value.PrivateKey, RsaHelper.ReadPrivateKeyFromPemFile(privateKeyPath));
    }

    /// <summary>
    /// 读取不存在的 PEM 文件抛文件未找到异常
    /// </summary>
    [Fact]
    public void ReadPublicKeyFromPemFile_WhenFileMissing_ThrowsFileNotFound()
    {
        var missingPath = Path.Combine(_tempDirectory, "missing.pem");

        Assert.Throws<FileNotFoundException>(() => { _ = RsaHelper.ReadPublicKeyFromPemFile(missingPath); });
        Assert.Throws<FileNotFoundException>(() => { _ = RsaHelper.ReadPrivateKeyFromPemFile(missingPath); });
    }

    /// <summary>
    /// PEM 形式的公钥可以直接用于加密（内部会自动剥离 PEM 头尾）
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Encrypt_WithPemPublicKey_RoundTrips()
    {
        var publicKeyPem = RsaHelper.ExportToPem(KeyPair.Value.PublicKey, "PUBLIC KEY");
        var privateKeyPem = RsaHelper.ExportToPem(KeyPair.Value.PrivateKey, "PRIVATE KEY");

        var cipher = RsaHelper.Encrypt(Payload, publicKeyPem);

        Assert.Equal(Payload, RsaHelper.Decrypt(cipher, privateKeyPem));
    }

    /// <summary>
    /// 私钥转 XML 后再导回来，仍能签出可被原公钥验证的签名
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void ConvertPrivateKeyToXmlAndBack_PreservesSigningKey()
    {
        var xml = RsaHelper.ConvertPrivateKeyToXml(KeyPair.Value.PrivateKey);

        Assert.Contains("<RSAKeyValue>", xml);
        Assert.Contains("<D>", xml);

        var restored = RsaHelper.ImportPrivateKeyFromXml(xml);
        var signature = RsaHelper.SignData(Payload, restored);

        Assert.True(RsaHelper.VerifyData(Payload, signature, KeyPair.Value.PublicKey));
    }

    /// <summary>
    /// 公钥转 XML 只含公开参数，导回后仍是 2048 位公钥
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void ConvertPublicKeyToXmlAndBack_KeepsPublicParametersOnly()
    {
        var xml = RsaHelper.ConvertPublicKeyToXml(KeyPair.Value.PublicKey);

        Assert.Contains("<Modulus>", xml);
        Assert.Contains("<Exponent>", xml);
        Assert.DoesNotContain("<D>", xml);

        var restored = RsaHelper.ImportPublicKeyFromXml(xml);

        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(restored), out _);
        Assert.Equal(2048, rsa.KeySize);
    }

    /// <summary>
    /// XML 形式的密钥对里，公钥不含私有指数、私钥含私有指数
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void GenerateKeysXml_SeparatesPublicAndPrivateParameters()
    {
        var (publicKeyXml, privateKeyXml) = RsaHelper.GenerateKeysXml();

        Assert.DoesNotContain("<D>", publicKeyXml);
        Assert.Contains("<D>", privateKeyXml);
    }

    /// <summary>
    /// 释放临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (IOException)
        {
            // 临时目录清理失败不应影响测试结论
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 按文档推荐方式生成一对 SubjectPublicKeyInfo / PKCS#8 密钥
    /// </summary>
    private static (string PublicKey, string PrivateKey) CreateSpkiKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
    }
}

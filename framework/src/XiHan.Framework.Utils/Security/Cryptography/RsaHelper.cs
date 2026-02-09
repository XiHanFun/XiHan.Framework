#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RsaHelper
// Guid:915cc6b5-156c-48d4-8dae-5bbf94d0c3d3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:04:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// RSA 加密算法助手类，提供加密、解密、签名和验证签名的功能
/// </summary>
/// <remarks>
/// <para>RSA 是一种基于大整数因式分解问题的非对称加密算法</para>
/// <para>本实现提供了：</para>
/// <list type="bullet">
/// <item>RSA 密钥生成（2048/4096 位）</item>
/// <item>多种密钥格式（Base64/PEM/XML）</item>
/// <item>分段加密/解密（支持大文本）</item>
/// <item>RSA+AES 混合加密（推荐生产使用）</item>
/// <item>数字签名与验证</item>
/// <item>多种填充模式（PKCS1/OAEP-SHA1/OAEP-SHA256）</item>
/// </list>
/// </remarks>
public static class RsaHelper
{
    #region 常量定义

    /// <summary>
    /// 最小安全密钥长度（位）
    /// </summary>
    private const int MinimumKeySize = 2048;

    /// <summary>
    /// 默认密钥长度（位）
    /// </summary>
    private const int DefaultKeySize = 2048;

    /// <summary>
    /// PKCS#1 v1.5 填充开销（字节）
    /// </summary>
    private const int Pkcs1PaddingOverhead = 11;

    /// <summary>
    /// OAEP-SHA1 填充开销（字节）
    /// </summary>
    private const int OaepSha1PaddingOverhead = 42;

    /// <summary>
    /// OAEP-SHA256 填充开销（字节）
    /// </summary>
    private const int OaepSha256PaddingOverhead = 66;

    /// <summary>
    /// OAEP-SHA512 填充开销（字节）
    /// </summary>
    private const int OaepSha512PaddingOverhead = 130;

    #endregion

    #region 密钥生成

    /// <summary>
    /// 生成 RSA 密钥对（Base64 格式）
    /// </summary>
    /// <param name="keySize">密钥长度（位），默认为 2048，推荐使用 2048 或 4096</param>
    /// <returns>返回公钥和私钥对（Base64 编码）</returns>
    /// <exception cref="ArgumentException">当密钥长度小于最小安全长度时抛出</exception>
    public static (string publicKey, string privateKey) GenerateKeys(int keySize = DefaultKeySize)
    {
        ValidateKeySize(keySize);
        var (publicKeyBytes, privateKeyBytes) = GenerateKeysBytes(keySize);
        return (Convert.ToBase64String(publicKeyBytes), Convert.ToBase64String(privateKeyBytes));
    }

    /// <summary>
    /// 生成 RSA 密钥对（字节数组格式）
    /// </summary>
    /// <param name="keySize">密钥长度（位），默认为 2048</param>
    /// <returns>返回公钥和私钥对（字节数组）</returns>
    /// <exception cref="ArgumentException">当密钥长度小于最小安全长度时抛出</exception>
    public static (byte[] publicKeyBytes, byte[] privateKeyBytes) GenerateKeysBytes(int keySize = DefaultKeySize)
    {
        ValidateKeySize(keySize);
        using var rsa = RSA.Create(keySize);
        return (rsa.ExportRSAPublicKey(), rsa.ExportRSAPrivateKey());
    }

    /// <summary>
    /// 生成 RSA 密钥对（PEM 格式）
    /// </summary>
    /// <param name="keySize">密钥长度（位），默认为 2048</param>
    /// <returns>返回公钥和私钥对（PEM 格式）</returns>
    public static (string publicKeyPem, string privateKeyPem) GenerateKeysPem(int keySize = DefaultKeySize)
    {
        var (publicKey, privateKey) = GenerateKeys(keySize);
        return (ExportToPem(publicKey, "PUBLIC KEY"), ExportToPem(privateKey, "PRIVATE KEY"));
    }

    /// <summary>
    /// 生成 RSA 密钥对（XML 格式）
    /// </summary>
    /// <param name="keySize">密钥长度（位），默认为 2048</param>
    /// <returns>返回 XML 格式的密钥</returns>
    public static (string publicKeyXml, string privateKeyXml) GenerateKeysXml(int keySize = DefaultKeySize)
    {
        ValidateKeySize(keySize);
        using var rsa = RSA.Create(keySize);
        return (rsa.ToXmlString(false), rsa.ToXmlString(true));
    }

    #endregion

    #region RSA 分段加密/解密

    /// <summary>
    /// 使用公钥加密数据（分段加密，适用于大文本）
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="publicKey">Base64 编码的公钥或 PEM 格式的公钥</param>
    /// <param name="padding">填充模式，默认为 OaepSHA256（推荐）</param>
    /// <param name="blockSize">自定义分块大小（字节），null 则自动计算</param>
    /// <returns>返回 Base64 编码的加密数据</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">加密过程中发生错误时抛出</exception>
    /// <remarks>
    /// 性能提示：
    /// <list type="bullet">
    /// <item>RSA 分段加密性能较差且密文体积大（约为明文的 8-10 倍）</item>
    /// <item>生产环境建议使用 EncryptWithAes 混合加密（快 10-100 倍）</item>
    /// <item>适用场景：小数据量、无需频繁加解密的场景</item>
    /// </list>
    /// </remarks>
    public static string Encrypt(string plainText, string publicKey, RSAEncryptionPadding? padding = null, int? blockSize = null)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        ArgumentNullException.ThrowIfNull(publicKey);

        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = EncryptBytes(plainTextBytes, publicKey, padding, blockSize);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 使用公钥加密字节数组（分段加密）
    /// </summary>
    /// <param name="plainBytes">要加密的字节数组</param>
    /// <param name="publicKey">Base64 编码的公钥或 PEM 格式的公钥</param>
    /// <param name="padding">填充模式，默认为 OaepSHA256</param>
    /// <param name="blockSize">自定义分块大小（字节），null 则自动计算</param>
    /// <returns>返回加密后的字节数组</returns>
    public static byte[] EncryptBytes(byte[] plainBytes, string publicKey, RSAEncryptionPadding? padding = null, int? blockSize = null)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);
        ArgumentNullException.ThrowIfNull(publicKey);

        try
        {
            using var rsa = CreateRsaFromPublicKey(publicKey);
            var effectivePadding = padding ?? RSAEncryptionPadding.OaepSHA256;
            var maxPlainTextBlockSize = blockSize ?? CalculateMaxPlainTextBlockSize(rsa.KeySize, effectivePadding);

            // 使用 ArrayPool 优化内存分配
            var cipherBlockSize = rsa.KeySize / 8;
            var blockCount = (plainBytes.Length + maxPlainTextBlockSize - 1) / maxPlainTextBlockSize;
            var resultBuffer = ArrayPool<byte>.Shared.Rent(blockCount * cipherBlockSize);

            try
            {
                var position = 0;
                var resultPosition = 0;

                while (position < plainBytes.Length)
                {
                    var currentBlockSize = Math.Min(maxPlainTextBlockSize, plainBytes.Length - position);
                    var plainBlock = plainBytes.AsSpan(position, currentBlockSize);
                    var cipherBlock = resultBuffer.AsSpan(resultPosition, cipherBlockSize);

                    if (!rsa.TryEncrypt(plainBlock, cipherBlock, effectivePadding, out var bytesWritten))
                    {
                        throw new CryptographicException("加密失败");
                    }

                    position += currentBlockSize;
                    resultPosition += bytesWritten;
                }

                // 复制结果
                var result = new byte[resultPosition];
                Array.Copy(resultBuffer, result, resultPosition);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(resultBuffer);
            }
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("加密过程中发生错误", ex);
        }
    }

    /// <summary>
    /// 使用私钥解密数据（分段解密）
    /// </summary>
    /// <param name="cipherText">Base64 编码的加密文本</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <param name="padding">填充模式，必须与加密时使用的一致，默认为 OaepSHA256</param>
    /// <returns>返回解密后的原文</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">解密过程中发生错误时抛出</exception>
    public static string Decrypt(string cipherText, string privateKey, RSAEncryptionPadding? padding = null)
    {
        ArgumentNullException.ThrowIfNull(cipherText);
        ArgumentNullException.ThrowIfNull(privateKey);

        var cipherBytes = Convert.FromBase64String(cipherText);
        var decryptedBytes = DecryptBytes(cipherBytes, privateKey, padding);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// 使用私钥解密字节数组（分段解密）
    /// </summary>
    /// <param name="cipherBytes">加密的字节数组</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <param name="padding">填充模式，默认为 OaepSHA256</param>
    /// <returns>返回解密后的字节数组</returns>
    public static byte[] DecryptBytes(byte[] cipherBytes, string privateKey, RSAEncryptionPadding? padding = null)
    {
        ArgumentNullException.ThrowIfNull(cipherBytes);
        ArgumentNullException.ThrowIfNull(privateKey);

        try
        {
            using var rsa = CreateRsaFromPrivateKey(privateKey);
            var effectivePadding = padding ?? RSAEncryptionPadding.OaepSHA256;
            var cipherBlockSize = rsa.KeySize / 8;

            if (cipherBytes.Length % cipherBlockSize != 0)
            {
                throw new CryptographicException("密文长度不正确");
            }

            var blockCount = cipherBytes.Length / cipherBlockSize;
            var resultBuffer = new List<byte>(cipherBytes.Length);

            for (var i = 0; i < blockCount; i++)
            {
                var offset = i * cipherBlockSize;
                var cipherBlock = cipherBytes.AsSpan(offset, cipherBlockSize);
                var decryptedBlock = rsa.Decrypt(cipherBlock.ToArray(), effectivePadding);
                resultBuffer.AddRange(decryptedBlock);
            }

            return [.. resultBuffer];
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("解密过程中发生错误", ex);
        }
    }

    #endregion

    #region RSA + AES 混合加密（推荐）

    /// <summary>
    /// 使用 RSA + AES 混合加密（推荐生产使用）
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="publicKey">Base64 编码的公钥或 PEM 格式的公钥</param>
    /// <returns>返回 Base64 编码的加密数据</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">加密过程中发生错误时抛出</exception>
    /// <remarks>
    /// <para>性能优势：比纯 RSA 分段加密快 10-100 倍</para>
    /// <para>体积优化：密文仅比明文增加约 512 字节（RSA 密文部分）</para>
    /// <para>安全性：AES-256-CBC + RSA-OAEP-SHA256 双重保护</para>
    /// <para>适用场景：大文本、文件、频繁加解密</para>
    /// </remarks>
    public static string EncryptWithAes(string plainText, string publicKey)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        ArgumentNullException.ThrowIfNull(publicKey);

        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = EncryptBytesWithAes(plainTextBytes, publicKey);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 使用 RSA + AES 混合加密字节数组（推荐生产使用）
    /// </summary>
    /// <param name="plainBytes">要加密的字节数组</param>
    /// <param name="publicKey">Base64 编码的公钥或 PEM 格式的公钥</param>
    /// <returns>返回加密后的字节数组</returns>
    public static byte[] EncryptBytesWithAes(byte[] plainBytes, string publicKey)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);
        ArgumentNullException.ThrowIfNull(publicKey);

        try
        {
            using var rsa = CreateRsaFromPublicKey(publicKey);

            // 1. 生成随机 AES 密钥（32 字节 = 256 位）
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateKey();
            aes.GenerateIV();

            // 2. 使用 RSA 加密 AES 密钥和 IV
            var encryptedAesKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
            var encryptedAesIV = rsa.Encrypt(aes.IV, RSAEncryptionPadding.OaepSHA256);

            // 3. 使用 AES 加密数据
            using var encryptor = aes.CreateEncryptor();
            var encryptedData = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // 4. 组合数据：[版本号 1 字节][RSA 密钥长度 2 字节][RSA(AESKey)][RSA(AESIV)][AES(Data)]
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer);

            writer.Write((byte)1); // 版本号，便于后续升级
            writer.Write((ushort)encryptedAesKey.Length);
            writer.Write(encryptedAesKey);
            writer.Write((ushort)encryptedAesIV.Length);
            writer.Write(encryptedAesIV);
            writer.Write(encryptedData);

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("混合加密过程中发生错误", ex);
        }
    }

    /// <summary>
    /// 使用 RSA + AES 混合解密（推荐生产使用）
    /// </summary>
    /// <param name="cipherText">Base64 编码的混合加密文本</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <returns>返回解密后的原文</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">解密过程中发生错误时抛出</exception>
    public static string DecryptWithAes(string cipherText, string privateKey)
    {
        ArgumentNullException.ThrowIfNull(cipherText);
        ArgumentNullException.ThrowIfNull(privateKey);

        var cipherBytes = Convert.FromBase64String(cipherText);
        var decryptedBytes = DecryptBytesWithAes(cipherBytes, privateKey);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// 使用 RSA + AES 混合解密字节数组（推荐生产使用）
    /// </summary>
    /// <param name="cipherBytes">加密的字节数组</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <returns>返回解密后的字节数组</returns>
    public static byte[] DecryptBytesWithAes(byte[] cipherBytes, string privateKey)
    {
        ArgumentNullException.ThrowIfNull(cipherBytes);
        ArgumentNullException.ThrowIfNull(privateKey);

        try
        {
            using var rsa = CreateRsaFromPrivateKey(privateKey);
            using var buffer = new MemoryStream(cipherBytes);
            using var reader = new BinaryReader(buffer);

            // 读取版本号
            var version = reader.ReadByte();
            if (version != 1)
            {
                throw new CryptographicException($"不支持的加密版本: {version}");
            }

            // 解析组合数据
            var encryptedKeyLength = reader.ReadUInt16();
            var encryptedAesKey = reader.ReadBytes(encryptedKeyLength);
            var encryptedIVLength = reader.ReadUInt16();
            var encryptedAesIV = reader.ReadBytes(encryptedIVLength);
            var encryptedData = reader.ReadBytes((int)(buffer.Length - buffer.Position));

            // 1. 使用 RSA 解密 AES 密钥和 IV
            var aesKey = rsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
            var aesIV = rsa.Decrypt(encryptedAesIV, RSAEncryptionPadding.OaepSHA256);

            // 2. 使用 AES 解密数据
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = aesIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("混合解密过程中发生错误", ex);
        }
    }

    #endregion

    #region 数字签名

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="data">要签名的数据</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <param name="hashAlgorithm">哈希算法，默认为 SHA256</param>
    /// <param name="padding">签名填充模式，默认为 Pkcs1</param>
    /// <returns>返回 Base64 编码的签名</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">签名过程中发生错误时抛出</exception>
    public static string SignData(string data, string privateKey, HashAlgorithmName? hashAlgorithm = null, RSASignaturePadding? padding = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(privateKey);

        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signatureBytes = SignDataBytes(dataBytes, privateKey, hashAlgorithm, padding);
        return Convert.ToBase64String(signatureBytes);
    }

    /// <summary>
    /// 使用私钥对字节数组进行签名
    /// </summary>
    /// <param name="dataBytes">要签名的字节数组</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <param name="hashAlgorithm">哈希算法，默认为 SHA256</param>
    /// <param name="padding">签名填充模式，默认为 Pkcs1</param>
    /// <returns>返回签名字节数组</returns>
    public static byte[] SignDataBytes(byte[] dataBytes, string privateKey, HashAlgorithmName? hashAlgorithm = null, RSASignaturePadding? padding = null)
    {
        ArgumentNullException.ThrowIfNull(dataBytes);
        ArgumentNullException.ThrowIfNull(privateKey);

        try
        {
            using var rsa = CreateRsaFromPrivateKey(privateKey);
            var algorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;
            var signaturePadding = padding ?? RSASignaturePadding.Pkcs1;
            return rsa.SignData(dataBytes, algorithm, signaturePadding);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("签名过程中发生错误", ex);
        }
    }

    /// <summary>
    /// 使用公钥验证签名
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <param name="signature">Base64 编码的签名</param>
    /// <param name="publicKey">Base64 编码的公钥或 PEM 格式的公钥</param>
    /// <param name="hashAlgorithm">哈希算法，默认为 SHA256</param>
    /// <param name="padding">签名填充模式，默认为 Pkcs1</param>
    /// <returns>返回签名是否有效</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    public static bool VerifyData(string data, string signature, string publicKey, HashAlgorithmName? hashAlgorithm = null, RSASignaturePadding? padding = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKey);

        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = Convert.FromBase64String(signature);
            return VerifyDataBytes(dataBytes, signatureBytes, publicKey, hashAlgorithm, padding);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// 使用公钥验证字节数组签名
    /// </summary>
    /// <param name="dataBytes">原始数据字节数组</param>
    /// <param name="signatureBytes">签名字节数组</param>
    /// <param name="publicKey">Base64 编码的公钥或 PEM 格式的公钥</param>
    /// <param name="hashAlgorithm">哈希算法，默认为 SHA256</param>
    /// <param name="padding">签名填充模式，默认为 Pkcs1</param>
    /// <returns>返回签名是否有效</returns>
    public static bool VerifyDataBytes(byte[] dataBytes, byte[] signatureBytes, string publicKey, HashAlgorithmName? hashAlgorithm = null, RSASignaturePadding? padding = null)
    {
        ArgumentNullException.ThrowIfNull(dataBytes);
        ArgumentNullException.ThrowIfNull(signatureBytes);
        ArgumentNullException.ThrowIfNull(publicKey);

        try
        {
            using var rsa = CreateRsaFromPublicKey(publicKey);
            var algorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;
            var signaturePadding = padding ?? RSASignaturePadding.Pkcs1;
            return rsa.VerifyData(dataBytes, signatureBytes, algorithm, signaturePadding);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    #endregion

    #region 密钥格式转换

    /// <summary>
    /// 将 Base64 密钥转换为 PEM 格式
    /// </summary>
    /// <param name="base64Key">Base64 编码的密钥</param>
    /// <param name="keyType">密钥类型（PUBLIC KEY 或 PRIVATE KEY）</param>
    /// <returns>返回 PEM 格式的密钥</returns>
    public static string ExportToPem(string base64Key, string keyType)
    {
        ArgumentNullException.ThrowIfNull(base64Key);
        ArgumentNullException.ThrowIfNull(keyType);

        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {keyType}-----");

        // 每 64 个字符换行
        for (var i = 0; i < base64Key.Length; i += 64)
        {
            var length = Math.Min(64, base64Key.Length - i);
            sb.AppendLine(base64Key.Substring(i, length));
        }

        sb.AppendLine($"-----END {keyType}-----");
        return sb.ToString();
    }

    /// <summary>
    /// 从 PEM 格式导入密钥
    /// </summary>
    /// <param name="pemString">PEM 格式的密钥字符串</param>
    /// <param name="keyType">密钥类型（PUBLIC KEY 或 PRIVATE KEY）</param>
    /// <returns>返回 Base64 编码的密钥</returns>
    /// <exception cref="FormatException">PEM 格式无效时抛出</exception>
    public static string ImportFromPem(string pemString, string keyType)
    {
        ArgumentNullException.ThrowIfNull(pemString);
        ArgumentNullException.ThrowIfNull(keyType);

        var header = $"-----BEGIN {keyType}-----";
        var footer = $"-----END {keyType}-----";

        var start = pemString.IndexOf(header, StringComparison.Ordinal);
        var end = pemString.IndexOf(footer, StringComparison.Ordinal);

        if (start == -1 || end == -1)
        {
            throw new FormatException($"无效的 PEM 格式，未找到 {keyType} 标记");
        }

        start += header.Length;
        var base64Content = pemString[start..end]
            .Replace(Environment.NewLine, "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace(" ", "")
            .Trim();

        return string.IsNullOrEmpty(base64Content)
            ? throw new FormatException("PEM 内容为空")
            : base64Content;
    }

    /// <summary>
    /// 将公钥转换为 XML 格式
    /// </summary>
    /// <param name="publicKey">Base64 编码的公钥</param>
    /// <returns>返回 XML 格式的公钥</returns>
    public static string ConvertPublicKeyToXml(string publicKey)
    {
        using var rsa = CreateRsaFromPublicKey(publicKey);
        return rsa.ToXmlString(false);
    }

    /// <summary>
    /// 将私钥转换为 XML 格式
    /// </summary>
    /// <param name="privateKey">Base64 编码的私钥</param>
    /// <returns>返回 XML 格式的私钥（包含公钥和私钥）</returns>
    public static string ConvertPrivateKeyToXml(string privateKey)
    {
        using var rsa = CreateRsaFromPrivateKey(privateKey);
        return rsa.ToXmlString(true);
    }

    /// <summary>
    /// 从 XML 格式导入公钥
    /// </summary>
    /// <param name="xmlKey">XML 格式的密钥</param>
    /// <returns>返回 Base64 编码的公钥</returns>
    public static string ImportPublicKeyFromXml(string xmlKey)
    {
        ArgumentNullException.ThrowIfNull(xmlKey);

        using var rsa = RSA.Create();
        rsa.FromXmlString(xmlKey);
        return Convert.ToBase64String(rsa.ExportRSAPublicKey());
    }

    /// <summary>
    /// 从 XML 格式导入私钥
    /// </summary>
    /// <param name="xmlKey">XML 格式的密钥（必须包含私钥）</param>
    /// <returns>返回 Base64 编码的私钥</returns>
    public static string ImportPrivateKeyFromXml(string xmlKey)
    {
        ArgumentNullException.ThrowIfNull(xmlKey);

        using var rsa = RSA.Create();
        rsa.FromXmlString(xmlKey);
        return Convert.ToBase64String(rsa.ExportRSAPrivateKey());
    }

    #endregion

    #region PEM 文件操作

    /// <summary>
    /// 将公钥保存为 PEM 文件
    /// </summary>
    /// <param name="publicKey">Base64 编码的公钥</param>
    /// <param name="filePath">保存文件的路径</param>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="IOException">文件操作失败时抛出</exception>
    public static void WritePublicKeyToPemFile(string publicKey, string filePath)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(filePath);

        var pem = ExportToPem(publicKey, "PUBLIC KEY");
        File.WriteAllText(filePath, pem, Encoding.UTF8);
    }

    /// <summary>
    /// 将私钥保存为 PEM 文件
    /// </summary>
    /// <param name="privateKey">Base64 编码的私钥</param>
    /// <param name="filePath">保存文件的路径</param>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="IOException">文件操作失败时抛出</exception>
    public static void WritePrivateKeyToPemFile(string privateKey, string filePath)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(filePath);

        var pem = ExportToPem(privateKey, "PRIVATE KEY");
        File.WriteAllText(filePath, pem, Encoding.UTF8);
    }

    /// <summary>
    /// 从 PEM 文件读取公钥
    /// </summary>
    /// <param name="filePath">PEM 文件路径</param>
    /// <returns>返回 Base64 编码的公钥</returns>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    /// <exception cref="FormatException">PEM 格式无效时抛出</exception>
    public static string ReadPublicKeyFromPemFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("指定的 PEM 文件不存在", filePath);
        }

        var pem = File.ReadAllText(filePath, Encoding.UTF8);
        return ImportFromPem(pem, "PUBLIC KEY");
    }

    /// <summary>
    /// 从 PEM 文件读取私钥
    /// </summary>
    /// <param name="filePath">PEM 文件路径</param>
    /// <returns>返回 Base64 编码的私钥</returns>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    /// <exception cref="FormatException">PEM 格式无效时抛出</exception>
    public static string ReadPrivateKeyFromPemFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("指定的 PEM 文件不存在", filePath);
        }

        var pem = File.ReadAllText(filePath, Encoding.UTF8);
        return ImportFromPem(pem, "PRIVATE KEY");
    }

    #endregion

    #region 密钥验证与工具

    /// <summary>
    /// 验证密钥对是否匹配（公钥和私钥是否配对）
    /// </summary>
    /// <param name="publicKey">Base64 编码的公钥</param>
    /// <param name="privateKey">Base64 编码的私钥</param>
    /// <returns>如果密钥对匹配返回 true，否则返回 false</returns>
    public static bool VerifyKeyPair(string publicKey, string privateKey)
    {
        try
        {
            // 使用私钥签名测试数据
            const string TestData = "KeyPairVerificationTest";
            var signature = SignData(TestData, privateKey);

            // 使用公钥验证签名
            return VerifyData(TestData, signature, publicKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取密钥长度（位）
    /// </summary>
    /// <param name="key">Base64 编码的公钥或私钥</param>
    /// <returns>返回密钥长度（位）</returns>
    public static int GetKeySize(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            // 尝试作为公钥加载
            using var rsa = CreateRsaFromPublicKey(key);
            return rsa.KeySize;
        }
        catch
        {
            // 尝试作为私钥加载
            using var rsa = CreateRsaFromPrivateKey(key);
            return rsa.KeySize;
        }
    }

    /// <summary>
    /// 从私钥导出公钥
    /// </summary>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <returns>返回 Base64 编码的公钥</returns>
    public static string ExportPublicKeyFromPrivateKey(string privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);

        using var rsa = CreateRsaFromPrivateKey(privateKey);
        return Convert.ToBase64String(rsa.ExportRSAPublicKey());
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 验证密钥大小是否合法
    /// </summary>
    private static void ValidateKeySize(int keySize)
    {
        if (keySize < MinimumKeySize)
        {
            throw new ArgumentException($"密钥长度必须大于或等于 {MinimumKeySize} 位", nameof(keySize));
        }

        if (keySize % 8 != 0)
        {
            throw new ArgumentException("密钥长度必须是 8 的倍数", nameof(keySize));
        }
    }

    /// <summary>
    /// 计算 RSA 最大明文块大小
    /// </summary>
    /// <param name="keySize">RSA 密钥长度（位）</param>
    /// <param name="padding">填充模式</param>
    /// <returns>最大明文块大小（字节）</returns>
    private static int CalculateMaxPlainTextBlockSize(int keySize, RSAEncryptionPadding padding)
    {
        var keySizeInBytes = keySize / 8;

        if (padding == RSAEncryptionPadding.Pkcs1)
        {
            return keySizeInBytes - Pkcs1PaddingOverhead;
        }
        else if (padding == RSAEncryptionPadding.OaepSHA1)
        {
            return keySizeInBytes - OaepSha1PaddingOverhead;
        }
        else if (padding == RSAEncryptionPadding.OaepSHA256)
        {
            return keySizeInBytes - OaepSha256PaddingOverhead;
        }
        else if (padding == RSAEncryptionPadding.OaepSHA512)
        {
            return keySizeInBytes - OaepSha512PaddingOverhead;
        }
        else
        {
            // 保守估计，使用 OAEP-SHA256
            return keySizeInBytes - OaepSha256PaddingOverhead;
        }
    }

    /// <summary>
    /// 从公钥创建 RSA 实例
    /// </summary>
    private static RSA CreateRsaFromPublicKey(string publicKey)
    {
        var publicKeyBytes = publicKey.Contains("-----BEGIN PUBLIC KEY-----")
            ? Convert.FromBase64String(ImportFromPem(publicKey, "PUBLIC KEY"))
            : Convert.FromBase64String(publicKey);

        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
        return rsa;
    }

    /// <summary>
    /// 从私钥创建 RSA 实例
    /// </summary>
    private static RSA CreateRsaFromPrivateKey(string privateKey)
    {
        var privateKeyBytes = privateKey.Contains("-----BEGIN PRIVATE KEY-----") || privateKey.Contains("-----BEGIN RSA PRIVATE KEY-----")
            ? Convert.FromBase64String(ImportFromPem(privateKey, privateKey.Contains("RSA PRIVATE KEY") ? "RSA PRIVATE KEY" : "PRIVATE KEY"))
            : Convert.FromBase64String(privateKey);

        var rsa = RSA.Create();

        try
        {
            // 尝试 PKCS#8 格式
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        }
        catch
        {
            // 尝试 PKCS#1 格式
            rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
        }

        return rsa;
    }

    #endregion
}

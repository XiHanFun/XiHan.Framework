#region<<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RsaHelper
// Guid:915cc6b5-156c-48d4-8dae-5bbf94d0c3d3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:04:12
// ----------------------------------------------------------------

#endregion<<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// RSA 加密算法助手类，提供加密、解密、签名和验证签名的功能
/// </summary>
/// <remarks>
/// RSA 是一种基于大整数因式分解问题的非对称加密算法。本实现提供了安全的加密解密方法和数字签名功能。
/// </remarks>
public static class RsaHelper
{
    // 最小安全密钥长度
    private const int MinimumKeySize = 2048;

    // 默认密钥长度
    private const int DefaultKeySize = 2048;

    // 分块加密时的默认分块大小
    private const int DefaultBlockSize = 214;

    // 分块加密时的默认分隔符
    private const string DefaultBlockSeparator = "|";

    /// <summary>
    /// 生成 RSA 密钥对
    /// </summary>
    /// <param name="keySize">密钥长度，默认为 2048 位</param>
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
    /// <param name="keySize">密钥长度，默认为 2048 位</param>
    /// <returns>返回公钥和私钥对（字节数组）</returns>
    /// <exception cref="ArgumentException">当密钥长度小于最小安全长度时抛出</exception>
    public static (byte[] publicKeyBytes, byte[] privateKeyBytes) GenerateKeysBytes(int keySize = DefaultKeySize)
    {
        ValidateKeySize(keySize);
        using var rsa = RSA.Create(keySize);
        return (rsa.ExportRSAPublicKey(), rsa.ExportRSAPrivateKey());
    }

    /// <summary>
    /// 使用公钥加密数据
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="publicKey">Base64 编码的公钥或 PEM 格式的公钥</param>
    /// <param name="blockSize">分块大小，默认为 214 字节（适用于 2048 位密钥）</param>
    /// <param name="blockSeparator">块分隔符，默认为 "|"，如果为 null 则不使用分隔符</param>
    /// <returns>返回 Base64 编码的加密数据</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">加密过程中发生错误时抛出</exception>
    public static string Encrypt(string plainText, string publicKey, int? blockSize = null, string? blockSeparator = DefaultBlockSeparator)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        ArgumentNullException.ThrowIfNull(publicKey);

        try
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var publicKeyBytes = publicKey.Contains("-----BEGIN PUBLIC KEY-----")
                ? Convert.FromBase64String(ImportFromPem(publicKey, "PUBLIC KEY"))
                : Convert.FromBase64String(publicKey);

            // 分块加密处理
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

            var effectiveBlockSize = blockSize ?? DefaultBlockSize;
            var encryptedBlocks = new List<string>();
            var position = 0;

            while (position < plainTextBytes.Length)
            {
                var currentBlockSize = Math.Min(effectiveBlockSize, plainTextBytes.Length - position);
                var blockData = new byte[currentBlockSize];
                Buffer.BlockCopy(plainTextBytes, position, blockData, 0, currentBlockSize);

                var encryptedBlock = rsa.Encrypt(blockData, RSAEncryptionPadding.Pkcs1);
                encryptedBlocks.Add(Convert.ToBase64String(encryptedBlock));

                position += currentBlockSize;
            }

            return blockSeparator != null
                ? string.Join(blockSeparator, encryptedBlocks)
                : string.Concat(encryptedBlocks);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("加密过程中发生错误", ex);
        }
    }

    /// <summary>
    /// 使用私钥解密数据
    /// </summary>
    /// <param name="cipherText">Base64 编码的加密文本</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <param name="blockSize">分块大小，默认为密钥长度/8</param>
    /// <param name="blockSeparator">块分隔符，默认为 "|"，如果为 null 则不使用分隔符</param>
    /// <returns>返回解密后的原文</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">解密过程中发生错误时抛出</exception>
    public static string Decrypt(string cipherText, string privateKey, int? blockSize = null, string? blockSeparator = DefaultBlockSeparator)
    {
        ArgumentNullException.ThrowIfNull(cipherText);
        ArgumentNullException.ThrowIfNull(privateKey);

        try
        {
            var privateKeyBytes = privateKey.Contains("-----BEGIN PRIVATE KEY-----")
                ? Convert.FromBase64String(ImportFromPem(privateKey, "PRIVATE KEY"))
                : Convert.FromBase64String(privateKey);

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

            var effectiveBlockSize = blockSize ?? (rsa.KeySize / 8);
            var buffer = new MemoryStream();

            // 分割加密块
            var encryptedBlocks = blockSeparator != null
                ? cipherText.Split(blockSeparator)
                : [cipherText];

            foreach (var block in encryptedBlocks)
            {
                var blockData = Convert.FromBase64String(block);
                var decryptedBlock = rsa.Decrypt(blockData, RSAEncryptionPadding.Pkcs1);
                buffer.Write(decryptedBlock, 0, decryptedBlock.Length);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("解密过程中发生错误", ex);
        }
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="data">要签名的数据</param>
    /// <param name="privateKey">Base64 编码的私钥或 PEM 格式的私钥</param>
    /// <param name="hashAlgorithm">哈希算法名称，默认为 SHA256</param>
    /// <returns>返回 Base64 编码的签名</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">签名过程中发生错误时抛出</exception>
    public static string SignData(string data, string privateKey, HashAlgorithmName? hashAlgorithm = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(privateKey);

        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var privateKeyBytes = privateKey.Contains("-----BEGIN PRIVATE KEY-----")
                ? Convert.FromBase64String(ImportFromPem(privateKey, "PRIVATE KEY"))
                : Convert.FromBase64String(privateKey);

            var algorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
            var signatureBytes = rsa.SignData(dataBytes, algorithm, RSASignaturePadding.Pkcs1);

            return Convert.ToBase64String(signatureBytes);
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
    /// <param name="hashAlgorithm">哈希算法名称，默认为 SHA256</param>
    /// <returns>返回签名是否有效</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="CryptographicException">验证过程中发生错误时抛出</exception>
    public static bool VerifyData(string data, string signature, string publicKey, HashAlgorithmName? hashAlgorithm = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKey);

        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = Convert.FromBase64String(signature);
            var publicKeyBytes = publicKey.Contains("-----BEGIN PUBLIC KEY-----")
                ? Convert.FromBase64String(ImportFromPem(publicKey, "PUBLIC KEY"))
                : Convert.FromBase64String(publicKey);

            var algorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            return rsa.VerifyData(dataBytes, signatureBytes, algorithm, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new CryptographicException("验证签名过程中发生错误", ex);
        }
    }

    /// <summary>
    /// 生成 RSA 公钥 PEM 文件
    /// </summary>
    /// <param name="publicKey">Base64 编码的公钥</param>
    /// <param name="filePath">保存公钥 PEM 文件的路径</param>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="IOException">文件操作失败时抛出</exception>
    public static void WritePublicKeyToPemFile(string publicKey, string filePath)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(filePath);

        var pem = ExportToPem(publicKey, "PUBLIC KEY");
        File.WriteAllText(filePath, pem);
    }

    /// <summary>
    /// 生成 RSA 私钥 PEM 文件
    /// </summary>
    /// <param name="privateKey">Base64 编码的私钥</param>
    /// <param name="filePath">保存私钥 PEM 文件的路径</param>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="IOException">文件操作失败时抛出</exception>
    public static void WritePrivateKeyToPemFile(string privateKey, string filePath)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(filePath);

        var pem = ExportToPem(privateKey, "PRIVATE KEY");
        File.WriteAllText(filePath, pem);
    }

    /// <summary>
    /// 从 PEM 文件读取公钥
    /// </summary>
    /// <param name="filePath">PEM 文件路径</param>
    /// <returns>返回 Base64 编码的公钥</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="IOException">文件操作失败时抛出</exception>
    /// <exception cref="FormatException">PEM 格式无效时抛出</exception>
    public static string ReadPublicKeyFromPemFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("指定的 PEM 文件不存在", filePath);
        }

        var pem = File.ReadAllText(filePath);
        return ImportFromPem(pem, "PUBLIC KEY");
    }

    /// <summary>
    /// 从 PEM 文件读取私钥
    /// </summary>
    /// <param name="filePath">PEM 文件路径</param>
    /// <returns>返回 Base64 编码的私钥</returns>
    /// <exception cref="ArgumentNullException">输入参数为空时抛出</exception>
    /// <exception cref="IOException">文件操作失败时抛出</exception>
    /// <exception cref="FormatException">PEM 格式无效时抛出</exception>
    public static string ReadPrivateKeyFromPemFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("指定的 PEM 文件不存在", filePath);
        }

        var pem = File.ReadAllText(filePath);
        return ImportFromPem(pem, "PRIVATE KEY");
    }

    /// <summary>
    /// 将密钥转换为 PEM 格式
    /// </summary>
    public static string ExportToPem(string base64String, string keyType)
    {
        ArgumentNullException.ThrowIfNull(base64String);
        ArgumentNullException.ThrowIfNull(keyType);

        return new StringBuilder()
            .AppendLine($"-----BEGIN {keyType}-----")
            .AppendLine(string.Join(Environment.NewLine,
                Enumerable.Range(0, (base64String.Length + 63) / 64)
                    .Select(i => base64String.Substring(i * 64, Math.Min(64, base64String.Length - (i * 64))))))
            .AppendLine($"-----END {keyType}-----")
            .ToString();
    }

    /// <summary>
    /// 从 PEM 格式导入密钥
    /// </summary>
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
            throw new FormatException("无效的 PEM 格式");
        }

        start += header.Length;
        var base64Content = pemString[start..end]
            .Replace(Environment.NewLine, "")
            .Replace("\n", "")
            .Trim();

        return string.IsNullOrEmpty(base64Content) ? throw new FormatException("PEM 内容为空") : base64Content;
    }

    #region 私有方法

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

    #endregion
}

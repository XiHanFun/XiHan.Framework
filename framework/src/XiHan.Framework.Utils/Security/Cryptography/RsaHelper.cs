#region<<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
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
/// Rsa 加密算法，用于加密、解密、签名和验证签名的功能
/// </summary>
/// <remarks>
/// 是一种基于大整数因式分解问题的非对称加密算法，它依赖于数学中两个大质数相乘形成的乘积很容易计算，但从乘积推导出这两个质数却极其困难的特性。
/// </remarks>
public static class RsaHelper
{
    /// <summary>
    /// 生成 RSA 密钥对
    /// </summary>
    /// <param name="keySize">密钥长度，默认为 2048</param>
    /// <returns>返回公钥和私钥对</returns>
    public static (string publicKey, string privateKey) GenerateKeys(int keySize = 2048)
    {
        var (publicKeyBytes, privateKeyBytes) = GenerateKeysBytes(keySize);
        return (Convert.ToBase64String(publicKeyBytes), Convert.ToBase64String(privateKeyBytes));
    }

    /// <summary>
    /// 生成 RSA 密钥对
    /// </summary>
    /// <param name="keySize">密钥长度，默认为 2048</param>
    /// <returns>返回公钥和私钥对</returns>
    public static (byte[] publicKeyBytes, byte[] privateKeyBytes) GenerateKeysBytes(int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var publicKeyBytes = rsa.ExportRSAPublicKey();
        var privateKeyBytes = rsa.ExportRSAPrivateKey();
        return (publicKeyBytes, privateKeyBytes);
    }

    /// <summary>
    /// 使用公钥加密数据
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="publicKey">公钥</param>
    /// <returns>返回加密后的数据</returns>
    public static string Encrypt(string plainText, string publicKey)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        var publicKeyBytes = Convert.FromBase64String(publicKey);
        var encryptedBytes = EncryptBytes(plainTextBytes, publicKeyBytes);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 使用公钥加密数据
    /// </summary>
    /// <param name="plainBytes">要加密的字节数组</param>
    /// <param name="publicKeyBytes">公钥字节数组</param>
    /// <returns>返回加密后的数据</returns>
    public static byte[] EncryptBytes(byte[] plainBytes, byte[] publicKeyBytes)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(publicKeyBytes, out _);
        return rsa.Encrypt(plainBytes, RSAEncryptionPadding.Pkcs1);
    }

    /// <summary>
    /// 使用私钥解密数据
    /// </summary>
    /// <param name="cipherText">要解密的文本</param>
    /// <param name="privateKey">私钥</param>
    /// <returns>返回解密后的数据</returns>
    public static string Decrypt(string cipherText, string privateKey)
    {
        var cipherTextBytes = Convert.FromBase64String(cipherText);
        var privateKeyBytes = Convert.FromBase64String(privateKey);
        var decryptedBytes = DecryptBytes(cipherTextBytes, privateKeyBytes);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// 使用私钥解密数据
    /// </summary>
    /// <param name="cipherBytes">要解密的字节数组</param>
    /// <param name="privateKeyBytes">私钥字节数组</param>
    /// <returns>返回解密后的数据</returns>
    public static byte[] DecryptBytes(byte[] cipherBytes, byte[] privateKeyBytes)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
        return rsa.Decrypt(cipherBytes, RSAEncryptionPadding.Pkcs1);
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="data">要签名的数据</param>
    /// <param name="privateKey">私钥</param>
    /// <returns>签名后的数据</returns>
    public static string SignData(string data, string privateKey)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var privateKeyBytes = Convert.FromBase64String(privateKey);
        var signedBytes = SignDataBytes(dataBytes, privateKeyBytes);
        return Convert.ToBase64String(signedBytes);
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="dataBytes">要签名的数据</param>
    /// <param name="privateKeyBytes">私钥</param>
    /// <returns>签名后的数据</returns>
    public static byte[] SignDataBytes(byte[] dataBytes, byte[] privateKeyBytes)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
        var signedBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return signedBytes;
    }

    /// <summary>
    /// 使用公钥验证签名
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <param name="signature">签名</param>
    /// <param name="publicKey">公钥</param>
    /// <returns>返回签名是否有效</returns>
    public static bool VerifyData(string data, string signature, string publicKey)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signatureBytes = Convert.FromBase64String(signature);
        var publicKeyBytes = Convert.FromBase64String(publicKey);
        return VerifyDataBytes(dataBytes, signatureBytes, publicKeyBytes);
    }

    /// <summary>
    /// 使用公钥验证签名
    /// </summary>
    /// <param name="dataBytes">原始数据</param>
    /// <param name="signatureBytes">签名</param>
    /// <param name="publicKeyBytes">公钥</param>
    /// <returns>返回签名是否有效</returns>
    public static bool VerifyDataBytes(byte[] dataBytes, byte[] signatureBytes, byte[] publicKeyBytes)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(publicKeyBytes, out _);
        return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// 生成 RSA 公钥 PEM 文件
    /// </summary>
    /// <param name="publicKeyBytes">公钥字节数组</param>
    /// <param name="filePath">保存公钥 PEM 文件的路径</param>
    public static void WritePublicKeyToPemFile(byte[] publicKeyBytes, string filePath)
    {
        var pem = ExportToPem(publicKeyBytes, "PUBLIC KEY");
        File.WriteAllText(filePath, pem);
    }

    /// <summary>
    /// 生成 RSA 私钥 PEM 文件
    /// </summary>
    /// <param name="privateKeyBytes">私钥字节数组</param>
    /// <param name="filePath">保存私钥 PEM 文件的路径</param>
    public static void WritePrivateKeyToPemFile(byte[] privateKeyBytes, string filePath)
    {
        var pem = ExportToPem(privateKeyBytes, "PRIVATE KEY");
        File.WriteAllText(filePath, pem);
    }

    /// <summary>
    /// 从 PEM 文件读取公钥
    /// </summary>
    /// <param name="filePath">PEM 文件路径</param>
    /// <returns>返回公钥字节数组</returns>
    public static byte[] ReadPublicKeyFromPemFile(string filePath)
    {
        var pem = File.ReadAllText(filePath);
        return ImportFromPem(pem, "PUBLIC KEY");
    }

    /// <summary>
    /// 从 PEM 文件读取私钥
    /// </summary>
    /// <param name="filePath">PEM 文件路径</param>
    /// <returns>返回私钥字节数组</returns>
    public static byte[] ReadPrivateKeyFromPemFile(string filePath)
    {
        var pem = File.ReadAllText(filePath);
        return ImportFromPem(pem, "PRIVATE KEY");
    }

    #region 私有方法

    /// <summary>
    /// 内部方法：将字节数组导出为 PEM 格式字符串
    /// </summary>
    /// <param name="keyBytes"></param>
    /// <param name="keyType"></param>
    /// <returns></returns>
    private static string ExportToPem(byte[] keyBytes, string keyType)
    {
        var base64 = Convert.ToBase64String(keyBytes);
        var sb = new StringBuilder();
        _ = sb.AppendLine($"-----BEGIN {keyType}-----");
        _ = sb.AppendLine(FormatBase64(base64));
        _ = sb.AppendLine($"-----END {keyType}-----");
        return sb.ToString();
    }

    /// <summary>
    /// 内部方法：从 PEM 格式字符串导入字节数组
    /// </summary>
    /// <param name="pem"></param>
    /// <param name="keyType"></param>
    /// <returns></returns>
    private static byte[] ImportFromPem(string pem, string keyType)
    {
        var header = $"-----BEGIN {keyType}-----";
        var footer = $"-----END {keyType}-----";
        var start = pem.IndexOf(header, StringComparison.Ordinal) + header.Length;
        var end = pem.IndexOf(footer, StringComparison.Ordinal);
        var base64 = pem[start..end].Trim();
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// 内部方法：格式化 Base64 字符串，分行显示
    /// </summary>
    /// <param name="base64"></param>
    /// <returns></returns>
    private static string FormatBase64(string base64)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < base64.Length; i += 64)
        {
            _ = sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        return sb.ToString();
    }

    #endregion
}

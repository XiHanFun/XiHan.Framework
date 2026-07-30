// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// Ecdsa 签名算法，只能用于签名和验证签名，无法用于数据加密和解密
/// </summary>
/// <remarks>
/// 是一种基于椭圆曲线离散对数问题的非对称加密算法，ECC 的具体应用之一，其安全性依赖于在椭圆曲线上的离散对数问题(Elliptic Curve Discrete Logarithm Problem, ECDLP)的计算难度
/// </remarks>
public static class EcdsaHelper
{
    /// <summary>
    /// 生成 ECDSA 密钥对
    /// </summary>
    /// <returns>返回公钥和私钥对</returns>
    public static (string publicKey, string privateKey) GenerateKeys()
    {
        var (publicKeyBytes, privateKeyBytes) = GenerateKeysBytes();
        return (Convert.ToBase64String(publicKeyBytes), Convert.ToBase64String(privateKeyBytes));
    }

    /// <summary>
    /// 生成 ECDSA 密钥对
    /// </summary>
    /// <returns>返回公钥和私钥对</returns>
    public static (byte[] publicKeyBytes, byte[] privateKeyBytes) GenerateKeysBytes()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
        var privateKeyBytes = ecdsa.ExportPkcs8PrivateKey();
        return (publicKeyBytes, privateKeyBytes);
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
        return SignDataBytes(dataBytes, privateKeyBytes);
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="dataBytes">要签名的数据</param>
    /// <param name="privateKeyBytes">私钥</param>
    /// <returns>签名后的数据</returns>
    public static string SignDataBytes(byte[] dataBytes, byte[] privateKeyBytes)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        var signature = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(signature);
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
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
        return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
    }
}

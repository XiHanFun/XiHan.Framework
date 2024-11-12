#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EcdsaHelper
// Guid:c463cb77-20b1-964e-f039-87cd6ffe4d52
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 5:28:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// Ecdsa 签名算法，只能用于签名和验证签名，无法用于数据加密和解密
/// </summary>
/// <remarks>
/// 是一种基于椭圆曲线离散对数问题的非对称加密算法，其安全性依赖于在椭圆曲线上的离散对数问题（Elliptic Curve Discrete Logarithm Problem, ECDLP）的计算难度。
/// </remarks>
public static class EcdsaHelper
{
    /// <summary>
    /// 生成ECDSA密钥对
    /// </summary>
    /// <returns>返回公钥和私钥对</returns>
    public static (string publicKey, string privateKey) GenerateKeys()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string? publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        string? privateKey = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
        return (publicKey, privateKey);
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="data">要签名的数据</param>
    /// <param name="privateKey">私钥</param>
    /// <returns>签名后的数据</returns>
    public static string SignData(string data, string privateKey)
    {
        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        byte[]? dataBytes = Encoding.UTF8.GetBytes(data);
        byte[]? signature = ecdsa.SignData(dataBytes, HashAlgorithmName.SHA256);
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
        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        byte[]? dataBytes = Encoding.UTF8.GetBytes(data);
        byte[]? signatureBytes = Convert.FromBase64String(signature);
        return ecdsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
    }
}

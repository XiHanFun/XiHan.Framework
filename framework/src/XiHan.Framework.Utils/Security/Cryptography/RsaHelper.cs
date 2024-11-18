#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RsaHelper
// Guid:915cc6b5-156c-48d4-8dae-5bbf94d0c3d3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:04:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    /// <param name="keySize">密钥大小，通常为2048或4096</param>
    /// <returns>返回公钥和私钥对</returns>
    public static (string publicKey, string privateKey) GenerateKeys(int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
        var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
        return (publicKey, privateKey);
    }

    /// <summary>
    /// 使用公钥加密数据
    /// </summary>
    /// <param name="plainText">要加密的明文</param>
    /// <param name="publicKey">公钥</param>
    /// <returns>加密后的密文</returns>
    public static string Encrypt(string plainText, string publicKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
        var encryptedBytes = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), RSAEncryptionPadding.Pkcs1);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 使用私钥解密数据
    /// </summary>
    /// <param name="cipherText">要解密的密文</param>
    /// <param name="privateKey">私钥</param>
    /// <returns>解密后的明文</returns>
    public static string Decrypt(string cipherText, string privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
        var decryptedBytes = rsa.Decrypt(Convert.FromBase64String(cipherText), RSAEncryptionPadding.Pkcs1);
        return Encoding.UTF8.GetString(decryptedBytes);
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="data">要签名的数据</param>
    /// <param name="privateKey">私钥</param>
    /// <returns>签名后的字符串</returns>
    public static string SignData(string data, string privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signedBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signedBytes);
    }

    /// <summary>
    /// 使用公钥验证签名
    /// </summary>
    /// <param name="data">要验证的数据</param>
    /// <param name="signature">签名</param>
    /// <param name="publicKey">公钥</param>
    /// <returns>签名是否有效</returns>
    public static bool VerifyData(string data, string signature, string publicKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signatureBytes = Convert.FromBase64String(signature);
        return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}

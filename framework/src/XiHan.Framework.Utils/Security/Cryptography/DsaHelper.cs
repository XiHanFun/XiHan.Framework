#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DsaHelper
// Guid:02dd6308-130e-43be-8642-48eeae0536e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 04:10:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// Dsa 签名算法辅助类
/// </summary>
/// <remarks>
/// 是一种基于离散对数问题的数字签名算法，主要用于签名和验证签名，而非加密数据
/// </remarks>
public static class DsaHelper
{
    /// <summary>
    /// 生成 Dsa 密钥对
    /// </summary>
    /// <returns>返回公钥和私钥对</returns>
    public static (string publicKey, string privateKey) GenerateKeys()
    {
        using var dsa = DSA.Create();
        var privateKey = Convert.ToBase64String(dsa.ExportPkcs8PrivateKey());
        var publicKey = Convert.ToBase64String(dsa.ExportSubjectPublicKeyInfo());
        return (publicKey, privateKey);
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="data">要签名的原始数据</param>
    /// <param name="privateKey">私钥(Base64 编码)</param>
    /// <returns>签名后的数据(Base64 编码)</returns>
    public static string SignData(string data, string privateKey)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var privateKeyBytes = Convert.FromBase64String(privateKey);
        return SignDataBytes(dataBytes, privateKeyBytes);
    }

    /// <summary>
    /// 使用私钥对数据进行签名(字节数据)
    /// </summary>
    /// <param name="dataBytes">要签名的原始字节数据</param>
    /// <param name="privateKeyBytes">私钥字节数据</param>
    /// <returns>签名后的数据(Base64 编码)</returns>
    public static string SignDataBytes(byte[] dataBytes, byte[] privateKeyBytes)
    {
        using var dsa = DSA.Create();
        dsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        var signature = dsa.SignData(dataBytes, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// 使用公钥验证签名
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <param name="signature">签名数据(Base64 编码)</param>
    /// <param name="publicKey">公钥(Base64 编码)</param>
    /// <returns>返回签名是否有效</returns>
    public static bool VerifyData(string data, string signature, string publicKey)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signatureBytes = Convert.FromBase64String(signature);
        var publicKeyBytes = Convert.FromBase64String(publicKey);
        return VerifyDataBytes(dataBytes, signatureBytes, publicKeyBytes);
    }

    /// <summary>
    /// 使用公钥验证签名(字节数据)
    /// </summary>
    /// <param name="dataBytes">原始字节数据</param>
    /// <param name="signatureBytes">签名字节数据</param>
    /// <param name="publicKeyBytes">公钥字节数据</param>
    /// <returns>返回签名是否有效</returns>
    public static bool VerifyDataBytes(byte[] dataBytes, byte[] signatureBytes, byte[] publicKeyBytes)
    {
        using var dsa = DSA.Create();
        dsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
        return dsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256);
    }
}

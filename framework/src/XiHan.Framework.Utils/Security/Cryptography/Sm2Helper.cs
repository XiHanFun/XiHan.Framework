#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Sm2Helper
// Guid:3f7d30f6-7ae6-4739-97f9-f5de56865077
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 4:19:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// 国密 SM2 算法辅助类
/// </summary>
/// <remarks>
/// 是一种基于椭圆曲线的公钥密码算法，适用于加密、签名等场景。
/// 本实现基于 BouncyCastle 提供的支持。
/// </remarks>
public static class Sm2Helper
{
    private static readonly X9ECParameters CurveParameters = SecNamedCurves.GetByName("sm2p256v1");

    private static readonly ECDomainParameters DomainParameters = new(CurveParameters.Curve, CurveParameters.G, CurveParameters.N, CurveParameters.H);

    /// <summary>
    /// 生成 SM2 密钥对
    /// </summary>
    /// <returns>公钥和私钥对</returns>
    public static (string publicKey, string privateKey) GenerateKeys()
    {
        var keyPair = GenerateKeyPair();
        // 导出私钥
        var privateKeyBytes = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private).GetEncoded();
        var privateKey = Convert.ToBase64String(privateKeyBytes);

        // 导出公钥
        var publicKeyBytes = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public).GetEncoded();
        var publicKey = Convert.ToBase64String(publicKeyBytes);
        return (publicKey, privateKey);
    }

    /// <summary>
    /// 使用私钥对数据进行签名
    /// </summary>
    /// <param name="data">要签名的数据</param>
    /// <param name="privateKey">私钥（Base64 编码）</param>
    /// <returns>签名后的数据（Base64 编码）</returns>
    public static string SignData(string data, string privateKey)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var privateKeyBytes = Convert.FromBase64String(privateKey);

        var privateKeyParam = new ECPrivateKeyParameters(new BigInteger(1, privateKeyBytes), DomainParameters);
        var signer = SignerUtilities.GetSigner("SM3WITHSM2");
        signer.Init(true, privateKeyParam);
        signer.BlockUpdate(dataBytes, 0, dataBytes.Length);

        var signature = signer.GenerateSignature();
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// 使用公钥验证签名
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <param name="signature">签名（Base64 编码）</param>
    /// <param name="publicKey">公钥（Base64 编码）</param>
    /// <returns>验证结果，true 表示签名有效</returns>
    public static bool VerifyData(string data, string signature, string publicKey)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signatureBytes = Convert.FromBase64String(signature);
        var publicKeyBytes = Convert.FromBase64String(publicKey);

        var q = CurveParameters.Curve.DecodePoint(publicKeyBytes);
        var publicKeyParam = new ECPublicKeyParameters(q, DomainParameters);

        var verifier = SignerUtilities.GetSigner("SM3WITHSM2");
        verifier.Init(false, publicKeyParam);
        verifier.BlockUpdate(dataBytes, 0, dataBytes.Length);

        return verifier.VerifySignature(signatureBytes);
    }

    /// <summary>
    /// 生成密钥对
    /// </summary>
    /// <returns>返回密钥对</returns>
    private static AsymmetricCipherKeyPair GenerateKeyPair()
    {
        var keyGen = GeneratorUtilities.GetKeyPairGenerator("EC");
        keyGen.Init(new ECKeyGenerationParameters(DomainParameters, new SecureRandom()));
        return keyGen.GenerateKeyPair();
    }
}
// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Text;

namespace XiHan.Framework.Security.Cryptography;

/// <summary>
/// 国密 SM2 算法辅助类
/// </summary>
/// <remarks>
/// 是一种基于椭圆曲线的公钥密码算法，适用于加密、签名等场景
/// 本实现基于 BouncyCastle 提供的支持
/// </remarks>
public static class Sm2Helper
{
    // SM2 椭圆曲线参数（GM 命名曲线，sm2p256v1）
    private static readonly X9ECParameters CurveParameters = GMNamedCurves.GetByName("sm2p256v1");

    // SM2 椭圆曲线域参数
    private static readonly ECDomainParameters DomainParameters = new(CurveParameters.Curve, CurveParameters.G, CurveParameters.N, CurveParameters.H);

    // SM2 默认用户标识（GB/T 32918 规定的缺省 ID）
    private static readonly byte[] Sm2UserId = Encoding.ASCII.GetBytes("1234567812345678");

    /// <summary>
    /// 生成 SM2 密钥对
    /// </summary>
    /// <returns>公钥和私钥对（Base64 编码的 DER）</returns>
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
    /// <param name="privateKey">私钥(Base64 编码的 DER)</param>
    /// <returns>签名后的数据(Base64 编码)</returns>
    public static string SignData(string data, string privateKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);

        var dataBytes = Encoding.UTF8.GetBytes(data);
        var privateKeyBytes = Convert.FromBase64String(privateKey);

        // 从 DER PrivateKeyInfo 解析出 EC 私钥参数（此前把整段 DER 当私钥大整数是错的）
        var privateKeyParam = (ECPrivateKeyParameters)PrivateKeyFactory.CreateKey(privateKeyBytes);

        var signer = new SM2Signer();
        signer.Init(true, new ParametersWithID(privateKeyParam, Sm2UserId));
        signer.BlockUpdate(dataBytes, 0, dataBytes.Length);

        var signature = signer.GenerateSignature();
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// 使用公钥验证签名
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <param name="signature">签名(Base64 编码)</param>
    /// <param name="publicKey">公钥(Base64 编码的 DER)</param>
    /// <returns>验证结果，true 表示签名有效</returns>
    public static bool VerifyData(string data, string signature, string publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);

        var dataBytes = Encoding.UTF8.GetBytes(data);
        var signatureBytes = Convert.FromBase64String(signature);
        var publicKeyBytes = Convert.FromBase64String(publicKey);

        // 从 DER SubjectPublicKeyInfo 解析出 EC 公钥参数（此前把整段 DER 当 EC 点是错的）
        var publicKeyParam = (ECPublicKeyParameters)PublicKeyFactory.CreateKey(publicKeyBytes);

        var verifier = new SM2Signer();
        verifier.Init(false, new ParametersWithID(publicKeyParam, Sm2UserId));
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

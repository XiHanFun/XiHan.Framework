#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EciesHelper
// Guid:2516349e-d36e-4b05-8881-659062d337bc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 2:10:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// Ecies 椭圆曲线加密解密
/// </summary>
/// <remarks>
/// 结合了 ECC 的密钥交换和对称加密算法（如 AES）来实现安全的加密通信。
/// </remarks>
public static class EciesHelper
{
    /// <summary>
    /// 生成椭圆曲线密钥对
    /// </summary>
    /// <returns>返回由私钥和公钥组成的密钥对</returns>
    public static (string privateKey, string publicKey) GenerateKeyPair()
    {
        var (privateKey, publicKey) = GenerateKeyPairBytes();
        return (Convert.ToBase64String(privateKey), Convert.ToBase64String(publicKey));
    }

    /// <summary>
    /// 生成椭圆曲线密钥对
    /// </summary>
    /// <returns>返回由私钥和公钥组成的密钥对</returns>
    public static (byte[] privateKey, byte[] publicKey) GenerateKeyPairBytes()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKey = ecdsa.ExportECPrivateKey();
        var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
        return (privateKey, publicKey);
    }

    /// <summary>
    /// 使用接收方的公钥加密消息
    /// </summary>
    /// <param name="receiverPublicKey">接收方的公钥</param>
    /// <param name="plainText">加密的消息</param>
    /// <returns>返回解密后的明文消息</returns>
    public static string Encrypt(string receiverPublicKey, string plainText)
    {
        var receiverPublicKeyBytes = Convert.FromBase64String(receiverPublicKey);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = EncryptBytes(receiverPublicKeyBytes, plainBytes);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 使用接收方的公钥加密消息
    /// </summary>
    /// <param name="receiverPublicKeyBytes">接收方的公钥</param>
    /// <param name="plainBytes">加密的消息</param>
    /// <returns>返回加密的消息</returns>
    public static byte[] EncryptBytes(byte[] receiverPublicKeyBytes, byte[] plainBytes)
    {
        // 生成发送方的临时密钥对
        using var senderEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        _ = senderEcdh.ExportECPrivateKey();
        var senderPublicKey = senderEcdh.ExportSubjectPublicKeyInfo();

        // 使用接收方的公钥生成共享密钥
        using var receiverEcdh = ECDiffieHellman.Create();
        receiverEcdh.ImportSubjectPublicKeyInfo(receiverPublicKeyBytes, out _);
        var sharedSecret = senderEcdh.DeriveKeyMaterial(receiverEcdh.PublicKey);

        // 使用共享密钥对消息进行加密（AES）
        using var aes = Aes.Create();
        aes.Key = sharedSecret;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // 返回加密结果：发送方公钥 + IV + 密文
        var result = new byte[senderPublicKey.Length + aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(senderPublicKey, 0, result, 0, senderPublicKey.Length);
        Buffer.BlockCopy(aes.IV, 0, result, senderPublicKey.Length, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, senderPublicKey.Length + aes.IV.Length, cipherBytes.Length);
        return result;
    }

    /// <summary>
    /// 使用接收方的私钥解密消息
    /// </summary>
    /// <param name="receiverPrivateKey">接收方的私钥</param>
    /// <param name="encryptedMessage">加密的消息</param>
    /// <returns>返回解密后的明文消息</returns>
    public static string Decrypt(string receiverPrivateKey, string encryptedMessage)
    {
        var receiverPrivateKeyBytes = Convert.FromBase64String(receiverPrivateKey);
        var encryptedBytes = Convert.FromBase64String(encryptedMessage);
        var plainBytes = DecryptBytes(receiverPrivateKeyBytes, encryptedBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// 使用接收方的私钥解密消息
    /// </summary>
    /// <param name="receiverPrivateKeyBytes">接收方的私钥</param>
    /// <param name="encryptedMessage">加密的消息</param>
    /// <returns>解密后的明文消息</returns>
    public static byte[] DecryptBytes(byte[] receiverPrivateKeyBytes, byte[] encryptedMessage)
    {
        // 提取发送方公钥
        using var senderEcdh = ECDiffieHellman.Create();
        var keySize = senderEcdh.KeySize / 8;
        var senderPublicKey = new byte[keySize];
        Buffer.BlockCopy(encryptedMessage, 0, senderPublicKey, 0, keySize);

        // 提取 AES IV 和密文
        const int IvSize = 16; // AES 固定的 IV 长度
        var iv = new byte[IvSize];
        var cipherBytes = new byte[encryptedMessage.Length - keySize - IvSize];
        Buffer.BlockCopy(encryptedMessage, keySize, iv, 0, IvSize);
        Buffer.BlockCopy(encryptedMessage, keySize + IvSize, cipherBytes, 0, cipherBytes.Length);

        // 使用接收方私钥和发送方公钥生成共享密钥
        using var receiverEcdh = ECDiffieHellman.Create();
        receiverEcdh.ImportECPrivateKey(receiverPrivateKeyBytes, out _);
        senderEcdh.ImportSubjectPublicKeyInfo(senderPublicKey, out _);
        var sharedSecret = receiverEcdh.DeriveKeyMaterial(senderEcdh.PublicKey);

        // 使用共享密钥对密文进行解密（AES）
        using var aes = Aes.Create();
        aes.Key = sharedSecret;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return plainBytes;
    }
}

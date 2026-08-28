// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// Ecies 椭圆曲线加密解密
/// </summary>
/// <remarks>
/// 结合了 ECC 的密钥交换和对称加密算法(如 AES)来实现安全的加密通信
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
        senderEcdh.ExportECPrivateKey();
        var senderPublicKey = senderEcdh.ExportSubjectPublicKeyInfo();

        // 使用接收方的公钥生成共享密钥
        using var receiverEcdh = ECDiffieHellman.Create();
        receiverEcdh.ImportSubjectPublicKeyInfo(receiverPublicKeyBytes, out _);
        var sharedSecret = senderEcdh.DeriveKeyMaterial(receiverEcdh.PublicKey);

        // 使用共享密钥对消息进行加密(AES)
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
        // 先用接收方私钥恢复接收方密钥实例
        using var receiverEcdh = ECDiffieHellman.Create();
        receiverEcdh.ImportECPrivateKey(receiverPrivateKeyBytes, out _);

        // 提取发送方公钥
        // 原缺陷：这里按 ECDiffieHellman.Create().KeySize / 8 推算头部长度，拿到的是默认曲线
        // (nistP521)的 65 字节；而 EncryptBytes 写进头部的是 nistP256 的 SubjectPublicKeyInfo(91 字节)，
        // 切片位置全错，后面导入发送方公钥必然抛 CryptographicException。
        // 密文格式(公钥 + IV + 密文，无长度前缀)不能改，所以改为按"同曲线的 SubjectPublicKeyInfo 编码等长"
        // 这一事实取长度：ECDH 要求收发双方在同一条曲线上，直接用接收方自己的公钥编码长度即可，
        // 既不改密文格式，也不把 nistP256 的 91 字节写死。
        var senderPublicKeyLength = receiverEcdh.ExportSubjectPublicKeyInfo().Length;
        var senderPublicKey = new byte[senderPublicKeyLength];
        Buffer.BlockCopy(encryptedMessage, 0, senderPublicKey, 0, senderPublicKeyLength);

        // 提取 AES IV 和密文
        const int IvSize = 16; // AES 固定的 IV 长度
        var iv = new byte[IvSize];
        var cipherBytes = new byte[encryptedMessage.Length - senderPublicKeyLength - IvSize];
        Buffer.BlockCopy(encryptedMessage, senderPublicKeyLength, iv, 0, IvSize);
        Buffer.BlockCopy(encryptedMessage, senderPublicKeyLength + IvSize, cipherBytes, 0, cipherBytes.Length);

        // 使用接收方私钥和发送方公钥生成共享密钥
        using var senderEcdh = ECDiffieHellman.Create();
        senderEcdh.ImportSubjectPublicKeyInfo(senderPublicKey, out _);
        var sharedSecret = receiverEcdh.DeriveKeyMaterial(senderEcdh.PublicKey);

        // 使用共享密钥对密文进行解密(AES)
        using var aes = Aes.Create();
        aes.Key = sharedSecret;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return plainBytes;
    }
}

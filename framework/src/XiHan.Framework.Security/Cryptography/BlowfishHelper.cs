#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BlowfishHelper
// Guid:46b52d28-fbdc-4a47-aa83-81b32bc2b43e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 04:15:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using System.Text;

namespace XiHan.Framework.Security.Cryptography;

/// <summary>
/// Blowfish 加密算法辅助类，用于数据的加密和解密
/// </summary>
/// <remarks>
/// 是一种对称加密算法，支持从 32 位到 448 位的可变密钥长度
/// 此实现依赖 BouncyCastle.Cryptography 库
/// </remarks>
public static class BlowfishHelper
{
    /// <summary>
    /// 使用 Blowfish 加密数据
    /// </summary>
    /// <param name="data">要加密的明文数据</param>
    /// <param name="key">密钥(建议 128 位或更长)</param>
    /// <returns>加密后的数据(Base64 编码)</returns>
    public static string Encrypt(string data, string key)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var cipherBytes = EncryptBytes(dataBytes, keyBytes);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// 使用 Blowfish 加密数据(字节级别)
    /// </summary>
    /// <param name="dataBytes">要加密的明文字节数据</param>
    /// <param name="keyBytes">密钥字节数据</param>
    /// <returns>加密后的密文字节数据</returns>
    public static byte[] EncryptBytes(byte[] dataBytes, byte[] keyBytes)
    {
        return ProcessCipher(dataBytes, keyBytes, true);
    }

    /// <summary>
    /// 使用 Blowfish 解密数据
    /// </summary>
    /// <param name="encryptedData">加密的密文数据(Base64 编码)</param>
    /// <param name="key">密钥(与加密时使用的密钥一致)</param>
    /// <returns>解密后的明文数据</returns>
    public static string Decrypt(string encryptedData, string key)
    {
        var cipherBytes = Convert.FromBase64String(encryptedData);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var plainBytes = DecryptBytes(cipherBytes, keyBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// 使用 Blowfish 解密数据(字节级别)
    /// </summary>
    /// <param name="cipherBytes">加密的密文字节数据</param>
    /// <param name="keyBytes">密钥字节数据</param>
    /// <returns>解密后的明文字节数据</returns>
    public static byte[] DecryptBytes(byte[] cipherBytes, byte[] keyBytes)
    {
        return ProcessCipher(cipherBytes, keyBytes, false);
    }

    /// <summary>
    /// 执行加密或解密操作
    /// </summary>
    /// <param name="inputBytes">输入数据(明文或密文)</param>
    /// <param name="keyBytes">密钥字节数据</param>
    /// <param name="forEncryption">指示是加密 (true) 还是解密 (false)</param>
    /// <returns>处理后的字节数据</returns>
    private static byte[] ProcessCipher(byte[] inputBytes, byte[] keyBytes, bool forEncryption)
    {
        // 限制密钥长度为 448 位以内
        if (keyBytes.Length > 56)
        {
            throw new ArgumentException("密钥长度必须为 448 位(56 字节)或更少。");
        }

        // 创建加密引擎
        var engine = new BlowfishEngine();
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine)); // 使用 CBC 模式

        // 初始化加密器
        var keyParam = new KeyParameter(keyBytes);
        cipher.Init(forEncryption, keyParam);

        // 处理数据
        var outputBytes = new byte[cipher.GetOutputSize(inputBytes.Length)];
        var length = cipher.ProcessBytes(inputBytes, 0, inputBytes.Length, outputBytes, 0);
        cipher.DoFinal(outputBytes, length);
        return outputBytes;
    }
}

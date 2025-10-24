#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AesHelper
// Guid:2494125d-816b-41f8-ba8e-7eadfa890095
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 5:25:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// Aes 加密解密
/// </summary>
/// <remarks>
/// 是一种对称密钥加密算法，广泛用于数据加密和保护
/// </remarks>
public static class AesHelper
{
    // AES KEY 的位数
    private const int KeySize = 256;

    // 加密块大小
    private const int BlockSize = 128;

    // 迭代次数
    private const int Iterations = 10000;

    /// <summary>
    /// 加密方法
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public static string Encrypt(string plainText, string password)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        // 生成盐
        var salt = new byte[BlockSize / 8];
        var keyBytes = DeriveKey(password, salt, KeySize / 8);
        var ivBytes = DeriveKey(password, salt, BlockSize / 8);
        var cipherBytes = EncryptBytes(plainBytes, keyBytes, ivBytes);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// 自定义 Key 和 IV 的加密方法
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="key">自定义的 Key</param>
    /// <param name="iv">自定义的 IV</param>
    /// <returns></returns>
    public static string Encrypt(string plainText, string key, string iv)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var ivBytes = Encoding.UTF8.GetBytes(iv);
        var cipherBytes = EncryptBytes(plainBytes, keyBytes, ivBytes);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// 自定义 Key 和 IV 的加密方法
    /// </summary>
    /// <param name="plainBytes">要加密的文本</param>
    /// <param name="keyBytes">自定义的 Key</param>
    /// <param name="ivBytes">自定义的 IV</param>
    /// <returns></returns>
    public static byte[] EncryptBytes(byte[] plainBytes, byte[] keyBytes, byte[] ivBytes)
    {
        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = ivBytes;

        // 加密算法
        using MemoryStream ms = new();
        using CryptoStream cs = new(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(plainBytes, 0, plainBytes.Length);
        cs.FlushFinalBlock();
        var cipherBytes = ms.ToArray();
        return cipherBytes;
    }

    /// <summary>
    /// 解密方法
    /// </summary>
    /// <param name="cipherText">要解密的文本</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string Decrypt(string cipherText, string password)
    {
        var cipherBytes = Convert.FromBase64String(cipherText);
        // 生成盐
        var salt = new byte[BlockSize / 8];
        var keyBytes = DeriveKey(password, salt, KeySize / 8);
        var ivBytes = DeriveKey(password, salt, BlockSize / 8);
        var plainBytes = DecryptBytes(cipherBytes, keyBytes, ivBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// 自定义 Key 和 IV 的解密方法
    /// </summary>
    /// <param name="cipherText">要解密的文本</param>
    /// <param name="key">自定义的 Key</param>
    /// <param name="iv">自定义的 IV</param>
    /// <returns></returns>
    public static string Decrypt(string cipherText, string key, string iv)
    {
        var cipherBytes = Convert.FromBase64String(cipherText);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var ivBytes = Encoding.UTF8.GetBytes(iv);
        var plainBytes = DecryptBytes(cipherBytes, keyBytes, ivBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// 自定义 Key 和 IV 的解密方法
    /// </summary>
    /// <param name="cipherBytes">要解密的数据</param>
    /// <param name="keyBytes">自定义的 Key</param>
    /// <param name="ivBytes">自定义的 IV</param>
    /// <returns></returns>
    public static byte[] DecryptBytes(byte[] cipherBytes, byte[] keyBytes, byte[] ivBytes)
    {
        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.IV = ivBytes;

        // 解密算法
        using MemoryStream ms = new();
        using CryptoStream cs = new(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cs.Write(cipherBytes, 0, cipherBytes.Length);
        cs.FlushFinalBlock();
        var plainBytes = ms.ToArray();
        return plainBytes;
    }

    /// <summary>
    /// 派生密钥
    /// </summary>
    /// <param name="password"></param>
    /// <param name="salt"></param>
    /// <param name="bytes"></param>
    /// <returns></returns>
    private static byte[] DeriveKey(string password, byte[] salt, int bytes)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: bytes
        );
    }
}

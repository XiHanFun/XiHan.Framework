#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
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

namespace XiHan.Framework.Utils.Encryptions;

/// <summary>
/// Aes 加密解密
/// </summary>
/// <remarks>
/// 是一种对称密钥加密算法，广泛用于数据加密和保护。
/// </remarks>
public static class AesHelper
{
    // AES KEY 的位数
    private const int KeySize = 256;

    // 加密块大小
    private const int BlockSize = 128;

    // 迭代次数
    private const int Iterations = 10000;

    // 分割器
    private static readonly char Separator = ':';

    /// <summary>
    /// 加密方法
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="password">密码</param>
    /// <returns></returns>
    public static string Encrypt(string plainText, string password)
    {
        // 生成盐
        var salt = new byte[BlockSize / 8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // 扩展密码为 IV 和 KEY
        var key = DeriveKey(password, salt, KeySize / 8);
        var iv = DeriveKey(password, salt, BlockSize / 8);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        // 加密算法
        string cipherText;
        using (MemoryStream cipherStream = new())
        {
            using CryptoStream cryptoStream = new(cipherStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            cryptoStream.Write(plainBytes, 0, plainBytes.Length);
            cryptoStream.FlushFinalBlock();
            var cipherBytes = cipherStream.ToArray();
            cipherText = Convert.ToBase64String(cipherBytes);
        }

        // 返回加密结果
        return $"{Convert.ToBase64String(salt)}{Separator}{cipherText}";
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
        // 检查密文的有效性
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentException("密码文本无效", nameof(cipherText));

        // 解析盐和密文
        var parts = cipherText.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new ArgumentException("密码文本无效", nameof(cipherText));

        var salt = Convert.FromBase64String(parts[0]);
        var cipherBytes = Convert.FromBase64String(parts[1]);

        // 扩展密码为 IV 和 KEY
        var key = DeriveKey(password, salt, KeySize / 8);
        var iv = DeriveKey(password, salt, BlockSize / 8);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        // 解密算法
        using MemoryStream plainStream = new();
        using CryptoStream cryptoStream = new(plainStream, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);
        cryptoStream.FlushFinalBlock();
        var plainBytes = plainStream.ToArray();
        var plainText = Encoding.UTF8.GetString(plainBytes);

        // 返回解密结果
        return plainText;
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
        using Rfc2898DeriveBytes pbkdf2 = new(password, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(bytes);
    }
}
#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HmacHelper
// Guid:8481bb9a-16db-41dd-9c56-1b4e5a01e6a5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/8 19:17:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// HMAC 辅助类
/// </summary>
/// <remarks>
/// 是基于哈希函数的消息认证码，用于验证消息的完整性和真实性。
/// HMAC 算法结合了一个密钥和一个哈希函数，生成消息的认证码（MAC），从而提供一种安全机制来防止消息被篡改。
/// </remarks>
public static class HmacHelper
{
    /// <summary>
    /// 使用 HmacSha1 算法生成 HMAC 值
    /// </summary>
    /// <param name="key">密钥（字节数组）</param>
    /// <param name="message">消息内容</param>
    /// <returns>HMAC 值（Base64 编码）</returns>
    public static string HmacSha1(string key, string message)
    {
        return ComputeHmac("HMACSHA1", key, message);
    }

    /// <summary>
    /// 使用 HmacSha256 算法生成 HMAC 值
    /// </summary>
    /// <param name="key">密钥（字节数组）</param>
    /// <param name="message">消息内容</param>
    /// <returns>HMAC 值（Base64 编码）</returns>
    public static string HmacSha256(string key, string message)
    {
        return ComputeHmac("HMACSHA256", key, message);
    }

    /// <summary>
    /// 使用 HmacSha384 算法生成 HMAC 值
    /// </summary>
    /// <param name="key">密钥（字节数组）</param>
    /// <param name="message">消息内容</param>
    /// <returns>HMAC 值（Base64 编码）</returns>
    public static string HmacSha384(string key, string message)
    {
        return ComputeHmac("HMACSHA384", key, message);
    }

    /// <summary>
    /// 使用 HmacSha512 算法生成 HMAC 值
    /// </summary>
    /// <param name="key">密钥（字节数组）</param>
    /// <param name="message">消息内容</param>
    /// <returns>HMAC 值（Base64 编码）</returns>
    public static string HmacSha512(string key, string message)
    {
        return ComputeHmac("HMACSHA512", key, message);
    }

    /// <summary>
    /// 使用指定的 HMAC 算法生成 HMAC 值
    /// </summary>
    /// <param name="algorithm">HMAC 算法类型，例如 "HMACSHA1", "HMACSHA256", "HMACSHA512"</param>
    /// <param name="key">密钥（字节数组）</param>
    /// <param name="message">消息内容</param>
    /// <returns>HMAC 值（Base64 编码）</returns>
    public static string ComputeHmac(string algorithm, string key, string message)
    {
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            throw new ArgumentException("算法名称不能为空", nameof(algorithm));
        }

        if (key is null || key.Length == 0)
        {
            throw new ArgumentException("密钥不能为空", nameof(key));
        }

        if (message is null)
        {
            throw new ArgumentException("消息不能为空", nameof(message));
        }

        var keyBytes = Convert.FromBase64String(key);
        using var hmac = CreateHmacAlgorithm(algorithm, keyBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 创建指定类型的 HMAC 实例
    /// </summary>
    /// <param name="algorithm">HMAC 算法类型</param>
    /// <param name="key">密钥</param>
    /// <returns>HMAC 实例</returns>
    private static HMAC CreateHmacAlgorithm(string algorithm, byte[] key)
    {
        return algorithm switch
        {
            "HMACSHA1" => new HMACSHA1(key),
            "HMACSHA256" => new HMACSHA256(key),
            "HMACSHA384" => new HMACSHA384(key),
            "HMACSHA512" => new HMACSHA512(key),
            _ => throw new NotSupportedException($"不支持的 HMAC 算法: {algorithm}")
        };
    }
}

﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DesHelper
// Guid:0be48243-b820-4de9-a46f-77f7e808dd6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 5:28:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// DES 加密解密
/// </summary>
/// <remarks>
/// 是一种对称密钥加密算法，已不推荐使用。
/// </remarks>
public static class DesHelper
{
    // 默认密匙
    private static readonly string defaultKey = "12345678";

    // 默认向量
    private static readonly string defaultIV = "87654321";

    /// <summary>
    /// 加密方法，只需提供明文文本
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns></returns>
    public static string Encrypt(string plainText)
    {
        var key = Encoding.UTF8.GetBytes(defaultKey);
        var iv = Encoding.UTF8.GetBytes(defaultIV);

        return Encrypt(plainText, key, iv);
    }

    /// <summary>
    /// 自定义 Key 和 IV 的加密方法
    /// </summary>
    /// <param name="plainText">要加密的文本</param>
    /// <param name="key">自定义的 Key</param>
    /// <param name="iv">自定义的 IV</param>
    /// <returns></returns>
    public static string Encrypt(string plainText, byte[] key, byte[] iv)
    {
        using var des = DES.Create();
        des.Key = key;
        des.IV = iv;

        var encryptor = des.CreateEncryptor();

        using MemoryStream ms = new();
        using CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write);
        var inputBytes = Encoding.UTF8.GetBytes(plainText);
        cs.Write(inputBytes, 0, inputBytes.Length);
        cs.FlushFinalBlock();
        var encryptedBytes = ms.ToArray();
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 解密方法，只需提供密文文本
    /// </summary>
    /// <param name="encryptedText"></param>
    /// <returns></returns>
    public static string Decrypt(string encryptedText)
    {
        var key = Encoding.UTF8.GetBytes(defaultKey);
        var iv = Encoding.UTF8.GetBytes(defaultIV);

        return Decrypt(encryptedText, key, iv);
    }

    /// <summary>
    /// 自定义 Key 和 IV 的解密方法
    /// </summary>
    /// <param name="encryptedText">要解密的文本</param>
    /// <param name="key">自定义的 Key</param>
    /// <param name="iv">自定义的 IV</param>
    /// <returns></returns>
    public static string Decrypt(string encryptedText, byte[] key, byte[] iv)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);

        using var des = DES.Create();
        des.Key = key;
        des.IV = iv;

        var decryptor = des.CreateDecryptor();

        using MemoryStream ms = new(encryptedBytes);
        using CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read);
        using StreamReader sr = new(cs);
        return sr.ReadToEnd();
    }
}

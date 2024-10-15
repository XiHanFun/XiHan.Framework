#region <<版权版本注释>>

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

namespace XiHan.Framework.Utils.Encryptions;

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
        byte[] key = Encoding.UTF8.GetBytes(defaultKey);
        byte[] iv = Encoding.UTF8.GetBytes(defaultIV);

        using DES des = DES.Create();
        des.Key = key;
        des.IV = iv;

        ICryptoTransform encryptor = des.CreateEncryptor();

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
        cs.Write(inputBytes, 0, inputBytes.Length);
        cs.FlushFinalBlock();
        byte[] encryptedBytes = ms.ToArray();
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// 解密方法，只需提供密文文本
    /// </summary>
    /// <param name="encryptedText"></param>
    /// <returns></returns>
    public static string Decrypt(string encryptedText)
    {
        byte[] key = Encoding.UTF8.GetBytes(defaultKey);
        byte[] iv = Encoding.UTF8.GetBytes(defaultIV);
        byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

        using DES des = DES.Create();
        des.Key = key;
        des.IV = iv;

        ICryptoTransform decryptor = des.CreateDecryptor();

        using var ms = new MemoryStream(encryptedBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
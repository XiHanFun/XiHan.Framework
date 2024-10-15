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

namespace XiHan.Framework.Utils.Encryptions;

/// <summary>
/// Des 加密解密
/// </summary>
/// <remarks>
/// 是一种对称密钥加密算法，广泛用于数据加密。
/// </remarks>
public static class DesHelper
{
    // 默认密码
    private static readonly string DefaultPassword = "ZhaiFanhua";

    // 迭代次数
    private const int Iterations = 10000;

    // 加密所需的密钥
    private static readonly byte[] KeyBytes;

    // 加密所需的密钥
    private static readonly byte[] IvBytes;

    /// <summary>
    /// 构造函数
    /// </summary>
    static DesHelper()
    {
        using var rdb = new Rfc2898DeriveBytes(DefaultPassword, new byte[8], Iterations, HashAlgorithmName.SHA256);
        KeyBytes = rdb.GetBytes(8);
        IvBytes = rdb.GetBytes(8);
    }

    /// <summary>
    /// 加密字符串
    /// </summary>
    /// <param name="plainText">待加密的明文字符串</param>
    /// <returns>返回加密后的字符串</returns>
    public static string Encrypt(string plainText)
    {
        using var des = DES.Create();
        des.Key = KeyBytes;
        des.IV = IvBytes;

        var encryptor = des.CreateEncryptor(des.Key, des.IV);

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        using var sw = new StreamWriter(cs);
        sw.Write(plainText);
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// 解密字符串
    /// </summary>
    /// <param name="cipherText">待解密的密文字符串</param>
    /// <returns>返回解密后的字符串</returns>
    public static string Decrypt(string cipherText)
    {
        using var des = DES.Create();
        des.Key = KeyBytes;
        des.IV = IvBytes;

        var decryptor = des.CreateDecryptor(des.Key, des.IV);

        using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
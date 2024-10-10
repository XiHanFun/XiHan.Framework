﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Md5HashEncryptionHelper
// Guid:bae8de8e-d5f1-4bad-b023-a1ec75174113
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:05:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Encryptions;

/// <summary>
/// Md5 生成哈希类
/// </summary>
public static class Md5HashEncryptionHelper
{
    /// <summary>
    /// 对字符串进行 MD5 生成哈希
    /// </summary>
    /// <param name="input">待加密的明文字符串</param>
    /// <returns>生成的哈希值</returns>
    public static string Encrypt(string input)
    {
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 对数据流进行 MD5 生成哈希
    /// </summary>
    /// <param name="inputPath">待加密的数据流路径</param>
    /// <returns>生成的哈希值</returns>
    public static string EncryptStream(string inputPath)
    {
        using FileStream stream = new(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hashBytes = MD5.HashData(stream);
        return Convert.ToHexString(hashBytes);
    }
}
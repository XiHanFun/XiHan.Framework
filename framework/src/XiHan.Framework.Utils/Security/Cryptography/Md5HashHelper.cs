#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Md5HashHelper
// Guid:bae8de8e-d5f1-4bad-b023-a1ec75174113
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 6:05:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// Md5 生成哈希
/// </summary>
/// <remarks>
/// 是一种哈希函数，它基于Merkle-Damgård结构，通过一系列的位运算、非线性函数、模加运算等步骤，将任意长度的输入消息压缩为一个固定长度的输出（128位的哈希值，也称为消息摘要）。
/// </remarks>
public static class Md5HashHelper
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
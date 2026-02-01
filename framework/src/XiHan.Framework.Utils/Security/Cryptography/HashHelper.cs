#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HashHelper
// Guid:8575ee1d-e5f7-464a-a85f-2926dd507546
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/11 06:03:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Utils.Security.Cryptography;

/// <summary>
/// 哈希生成辅助类
/// </summary>
/// <remarks>
/// 是一系列加密哈希函数，主要用于生成数据的固定长度散列值，以确保数据完整性和安全性
/// </remarks>
public static class HashHelper
{
    /// <summary>
    /// 生成 SHA1 哈希值
    /// </summary>
    /// <param name="data">待加密的数据</param>
    /// <returns>生成的哈希值</returns>
    public static string Sha1(string data)
    {
        // 创建 SHA256 加密算法实例，将字符串数据转换为字节数组，并生成相应的哈希值
        var hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 生成 SHA256 哈希值
    /// </summary>
    /// <param name="data">待加密的数据</param>
    /// <returns>生成的哈希值</returns>
    public static string Sha256(string data)
    {
        // 创建 SHA256 加密算法实例，将字符串数据转换为字节数组，并生成相应的哈希值
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 生成 SHA384 哈希值
    /// </summary>
    /// <param name="data">待加密的数据</param>
    /// <returns>生成的哈希值</returns>
    public static string Sha384(string data)
    {
        // 创建 SHA384 加密算法实例，将字符串数据转换为字节数组，并生成相应的哈希值
        var hashBytes = SHA384.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 生成 SHA512 哈希值
    /// </summary>
    /// <param name="data">待加密的数据</param>
    /// <returns>生成的哈希值</returns>
    public static string Sha512(string data)
    {
        // 创建 SHA512 加密算法实例，将字符串数据转换为字节数组，并生成相应的哈希值
        var hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 对字符串进行 MD5 生成哈希
    /// </summary>
    /// <param name="input">待加密的明文字符串</param>
    /// <returns>生成的哈希值</returns>
    public static string Md5(string input)
    {
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 对数据流进行 MD5 生成哈希
    /// </summary>
    /// <param name="inputPath">待加密的数据流路径</param>
    /// <returns>生成的哈希值</returns>
    public static string StreamMd5(string inputPath)
    {
        using FileStream stream = new(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return StreamMd5(stream);
    }

    /// <summary>
    /// 对数据流进行 MD5 生成哈希
    /// </summary>
    /// <param name="stream">待加密的数据流</param>
    /// <returns>生成的哈希值</returns>
    public static string StreamMd5(Stream stream)
    {
        var hashBytes = MD5.HashData(stream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 对数据流进行 SHA256 生成哈希
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string StreamHash(Stream data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 对二进制数据进行 MD5 生成哈希
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string ByteMd5(byte[] data)
    {
        var hashBytes = MD5.HashData(data);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// 对二进制数据进行 SHA256 生成哈希
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string ByteHash(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes);
    }
}

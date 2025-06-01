#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GuidHelper
// Guid:17656887-6e53-4051-9bf8-066e1951835e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 6:13:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Numerics;

namespace XiHan.Framework.Utils.System;

/// <summary>
/// Guid帮助类
/// </summary>
public static class GuidHelper
{
    /// <summary>
    /// 生成一个新的 GUID。
    /// </summary>
    /// <returns>标准格式的 GUID 字符串。</returns>
    public static string GenerateGuid()
    {
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 验证字符串是否是有效的 GUID。
    /// </summary>
    /// <param name="input">输入字符串。</param>
    /// <returns>如果是有效 GUID，返回 true；否则返回 false。</returns>
    public static bool IsValidGuid(string input)
    {
        return Guid.TryParse(input, out _);
    }

    /// <summary>
    /// 从字符串解析 GUID。
    /// </summary>
    /// <param name="input">输入字符串。</param>
    /// <returns>如果解析成功，返回 GUID；否则返回 Guid.Empty。</returns>
    public static Guid ParseGuid(string input)
    {
        return Guid.TryParse(input, out var result) ? result : Guid.Empty;
    }

    /// <summary>
    /// 将 GUID 转换为指定的格式。
    /// </summary>
    /// <param name="guid">GUID。</param>
    /// <param name="format">格式化字符串(N、D、B、P、X)。</param>
    /// <returns>格式化后的 GUID 字符串。</returns>
    public static string FormatGuid(Guid guid, string format = "D")
    {
        return guid.ToString(format);
    }

    /// <summary>
    /// 根据 GUID 获取唯一的长整型数字序列。
    /// </summary>
    /// <param name="guid">输入的 GUID。</param>
    /// <returns>长整型数字序列。</returns>
    public static long GetUniqueLong(Guid guid)
    {
        var bytes = guid.ToByteArray();
        // 取前 8 个字节并转换为长整型
        return BitConverter.ToInt64(bytes, 0);
    }

    /// <summary>
    /// 根据 GUID 获取唯一的大数字序列(BigInteger)。
    /// </summary>
    /// <param name="guid">输入的 GUID。</param>
    /// <returns>BigInteger 表示的唯一数字。</returns>
    public static BigInteger GetUniqueBigInteger(Guid guid)
    {
        var bytes = guid.ToByteArray();
        // 将字节数组扩展为正数表示(确保最高位是 0，避免负数)
        Array.Resize(ref bytes, bytes.Length + 1);
        return new BigInteger(bytes);
    }

    /// <summary>
    /// 比较两个 GUID 是否相等。
    /// </summary>
    /// <param name="guid1">第一个 GUID。</param>
    /// <param name="guid2">第二个 GUID。</param>
    /// <returns>如果两个 GUID 相等，返回 true；否则返回 false。</returns>
    public static bool AreEqual(Guid guid1, Guid guid2)
    {
        return guid1.Equals(guid2);
    }

    /// <summary>
    /// 检查 GUID 是否为空(Guid.Empty)。
    /// </summary>
    /// <param name="guid">GUID。</param>
    /// <returns>如果 GUID 为空，返回 true；否则返回 false。</returns>
    public static bool IsEmpty(Guid guid)
    {
        return guid == Guid.Empty;
    }

    /// <summary>
    /// 生成一个短 GUID(Base64 编码)。
    /// </summary>
    /// <returns>短 GUID 字符串。</returns>
    public static string ToShortGuid()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .TrimEnd('=');
    }

    /// <summary>
    /// 从短 GUID(Base64 编码)转换为标准 GUID。
    /// </summary>
    /// <param name="shortGuid">短 GUID。</param>
    /// <returns>标准 GUID。如果转换失败，返回 Guid.Empty。</returns>
    public static Guid FromShortGuid(string shortGuid)
    {
        try
        {
            var base64 = shortGuid
                .Replace("_", "/")
                .Replace("-", "+")
                .PadRight(22, '=');
            var bytes = Convert.FromBase64String(base64);
            return new Guid(bytes);
        }
        catch
        {
            return Guid.Empty;
        }
    }
}

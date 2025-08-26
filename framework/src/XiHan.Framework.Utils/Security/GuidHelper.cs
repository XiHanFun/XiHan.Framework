#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GuidHelper
// Guid:a1b2c3d4-e5f6-7890-abcd-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 10:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Security.Cryptography;

/// <summary>
/// Guid 生成和操作辅助类
/// </summary>
/// <remarks>
/// 提供各种格式的全局唯一标识符生成、验证、转换和解析功能，支持标准Guid、时间戳Guid、确定性Guid等。
/// </remarks>
public static class GuidHelper
{
    /// <summary>
    /// Guid 格式的正则表达式
    /// </summary>
    private static readonly Regex GuidRegex = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    /// <summary>
    /// 无连字符的Guid格式正则表达式
    /// </summary>
    private static readonly Regex GuidNoDashRegex = new(
        @"^[0-9a-fA-F]{32}$",
        RegexOptions.Compiled);

    /// <summary>
    /// 生成标准的随机 Guid
    /// </summary>
    /// <returns>新的 Guid 实例</returns>
    public static Guid NewGuid()
    {
        return Guid.NewGuid();
    }

    /// <summary>
    /// 生成加密安全的随机 Guid
    /// </summary>
    /// <returns>基于加密随机数生成的 Guid</returns>
    public static Guid NewCryptoGuid()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[16];
        rng.GetBytes(bytes);
        
        // 设置版本为4（随机生成）
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        // 设置变体
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        
        return new Guid(bytes);
    }

    /// <summary>
    /// 基于当前时间戳生成 Guid（时间有序）
    /// </summary>
    /// <returns>基于时间戳的 Guid</returns>
    public static Guid NewTimeBasedGuid()
    {
        // 获取当前时间的 ticks
        var ticks = DateTime.UtcNow.Ticks;
        var ticksBytes = BitConverter.GetBytes(ticks);
        
        // 生成随机字节填充剩余部分
        using var rng = RandomNumberGenerator.Create();
        var randomBytes = new byte[8];
        rng.GetBytes(randomBytes);
        
        // 组合时间戳和随机字节
        var guidBytes = new byte[16];
        Array.Copy(ticksBytes, 0, guidBytes, 0, 8);
        Array.Copy(randomBytes, 0, guidBytes, 8, 8);
        
        // 设置版本为1（时间基础）
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x10);
        // 设置变体
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        
        return new Guid(guidBytes);
    }

    /// <summary>
    /// 基于字符串生成确定性 Guid（相同输入产生相同 Guid）
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <param name="namespaceGuid">命名空间 Guid，默认为空 Guid</param>
    /// <returns>基于输入生成的确定性 Guid</returns>
    public static Guid NewDeterministicGuid(string input, Guid? namespaceGuid = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        var ns = namespaceGuid ?? Guid.Empty;
        var namespaceBytes = ns.ToByteArray();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        
        // 组合命名空间和输入
        var combinedBytes = new byte[namespaceBytes.Length + inputBytes.Length];
        Array.Copy(namespaceBytes, 0, combinedBytes, 0, namespaceBytes.Length);
        Array.Copy(inputBytes, 0, combinedBytes, namespaceBytes.Length, inputBytes.Length);
        
        // 使用 SHA1 哈希生成
        var hashBytes = SHA1.HashData(combinedBytes);
        
        // 取前16字节作为 Guid
        var guidBytes = new byte[16];
        Array.Copy(hashBytes, 0, guidBytes, 0, 16);
        
        // 设置版本为5（基于名称的SHA1）
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        // 设置变体
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        
        return new Guid(guidBytes);
    }

    /// <summary>
    /// 基于字符串生成确定性 Guid（使用 MD5）
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <param name="namespaceGuid">命名空间 Guid，默认为空 Guid</param>
    /// <returns>基于输入生成的确定性 Guid</returns>
    public static Guid NewDeterministicGuidMd5(string input, Guid? namespaceGuid = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        var ns = namespaceGuid ?? Guid.Empty;
        var namespaceBytes = ns.ToByteArray();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        
        // 组合命名空间和输入
        var combinedBytes = new byte[namespaceBytes.Length + inputBytes.Length];
        Array.Copy(namespaceBytes, 0, combinedBytes, 0, namespaceBytes.Length);
        Array.Copy(inputBytes, 0, combinedBytes, namespaceBytes.Length, inputBytes.Length);
        
        // 使用 MD5 哈希生成
        var hashBytes = MD5.HashData(combinedBytes);
        
        // 设置版本为3（基于名称的MD5）
        hashBytes[6] = (byte)((hashBytes[6] & 0x0F) | 0x30);
        // 设置变体
        hashBytes[8] = (byte)((hashBytes[8] & 0x3F) | 0x80);
        
        return new Guid(hashBytes);
    }

    /// <summary>
    /// 验证字符串是否为有效的 Guid 格式
    /// </summary>
    /// <param name="guidString">要验证的字符串</param>
    /// <returns>是否为有效的 Guid 格式</returns>
    public static bool IsValidGuid(string guidString)
    {
        if (string.IsNullOrWhiteSpace(guidString))
        {
            return false;
        }
        
        return Guid.TryParse(guidString, out _);
    }

    /// <summary>
    /// 验证字符串是否为标准的 Guid 格式（带连字符）
    /// </summary>
    /// <param name="guidString">要验证的字符串</param>
    /// <returns>是否为标准的 Guid 格式</returns>
    public static bool IsValidStandardGuid(string guidString)
    {
        if (string.IsNullOrWhiteSpace(guidString))
        {
            return false;
        }
        
        return GuidRegex.IsMatch(guidString);
    }

    /// <summary>
    /// 验证字符串是否为无连字符的 Guid 格式
    /// </summary>
    /// <param name="guidString">要验证的字符串</param>
    /// <returns>是否为无连字符的 Guid 格式</returns>
    public static bool IsValidGuidNoDash(string guidString)
    {
        if (string.IsNullOrWhiteSpace(guidString))
        {
            return false;
        }
        
        return GuidNoDashRegex.IsMatch(guidString);
    }

    /// <summary>
    /// 尝试解析字符串为 Guid
    /// </summary>
    /// <param name="guidString">要解析的字符串</param>
    /// <param name="guid">解析结果</param>
    /// <returns>解析是否成功</returns>
    public static bool TryParse(string guidString, out Guid guid)
    {
        return Guid.TryParse(guidString, out guid);
    }

    /// <summary>
    /// 解析字符串为 Guid，失败时抛出异常
    /// </summary>
    /// <param name="guidString">要解析的字符串</param>
    /// <returns>解析后的 Guid</returns>
    /// <exception cref="ArgumentNullException">输入字符串为空时抛出</exception>
    /// <exception cref="FormatException">格式无效时抛出</exception>
    public static Guid Parse(string guidString)
    {
        ArgumentNullException.ThrowIfNull(guidString);
        return Guid.Parse(guidString);
    }

    /// <summary>
    /// 将 Guid 转换为不同格式的字符串
    /// </summary>
    /// <param name="guid">要转换的 Guid</param>
    /// <param name="format">格式，支持 N、D、B、P、X</param>
    /// <returns>格式化后的字符串</returns>
    public static string ToString(Guid guid, string format = "D")
    {
        return guid.ToString(format);
    }

    /// <summary>
    /// 将 Guid 转换为无连字符的字符串
    /// </summary>
    /// <param name="guid">要转换的 Guid</param>
    /// <returns>无连字符的字符串</returns>
    public static string ToStringNoDash(Guid guid)
    {
        return guid.ToString("N");
    }

    /// <summary>
    /// 将 Guid 转换为大写字符串
    /// </summary>
    /// <param name="guid">要转换的 Guid</param>
    /// <param name="format">格式，支持 N、D、B、P、X</param>
    /// <returns>大写格式化后的字符串</returns>
    public static string ToUpperString(Guid guid, string format = "D")
    {
        return guid.ToString(format).ToUpperInvariant();
    }

    /// <summary>
    /// 将 Guid 转换为小写字符串
    /// </summary>
    /// <param name="guid">要转换的 Guid</param>
    /// <param name="format">格式，支持 N、D、B、P、X</param>
    /// <returns>小写格式化后的字符串</returns>
    public static string ToLowerString(Guid guid, string format = "D")
    {
        return guid.ToString(format).ToLowerInvariant();
    }

    /// <summary>
    /// 将无连字符的 Guid 字符串转换为标准格式
    /// </summary>
    /// <param name="guidNoDash">无连字符的 Guid 字符串</param>
    /// <returns>标准格式的 Guid 字符串</returns>
    /// <exception cref="ArgumentException">输入格式无效时抛出</exception>
    public static string ToStandardFormat(string guidNoDash)
    {
        if (!IsValidGuidNoDash(guidNoDash))
        {
            throw new ArgumentException("输入不是有效的无连字符 Guid 格式", nameof(guidNoDash));
        }
        
        var guid = Guid.Parse(guidNoDash);
        return guid.ToString("D");
    }

    /// <summary>
    /// 将标准格式的 Guid 字符串转换为无连字符格式
    /// </summary>
    /// <param name="standardGuid">标准格式的 Guid 字符串</param>
    /// <returns>无连字符格式的 Guid 字符串</returns>
    /// <exception cref="ArgumentException">输入格式无效时抛出</exception>
    public static string ToNoDashFormat(string standardGuid)
    {
        if (!IsValidStandardGuid(standardGuid))
        {
            throw new ArgumentException("输入不是有效的标准 Guid 格式", nameof(standardGuid));
        }
        
        var guid = Guid.Parse(standardGuid);
        return guid.ToString("N");
    }

    /// <summary>
    /// 获取 Guid 的字节数组
    /// </summary>
    /// <param name="guid">要转换的 Guid</param>
    /// <returns>字节数组</returns>
    public static byte[] ToByteArray(Guid guid)
    {
        return guid.ToByteArray();
    }

    /// <summary>
    /// 从字节数组创建 Guid
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <returns>创建的 Guid</returns>
    /// <exception cref="ArgumentException">字节数组长度不正确时抛出</exception>
    public static Guid FromByteArray(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        
        if (bytes.Length != 16)
        {
            throw new ArgumentException("字节数组长度必须为16", nameof(bytes));
        }
        
        return new Guid(bytes);
    }

    /// <summary>
    /// 获取 Guid 的版本号
    /// </summary>
    /// <param name="guid">要检查的 Guid</param>
    /// <returns>版本号（1-5）</returns>
    public static int GetVersion(Guid guid)
    {
        var bytes = guid.ToByteArray();
        return (bytes[7] & 0xF0) >> 4;
    }

    /// <summary>
    /// 获取 Guid 的变体
    /// </summary>
    /// <param name="guid">要检查的 Guid</param>
    /// <returns>变体值</returns>
    public static int GetVariant(Guid guid)
    {
        var bytes = guid.ToByteArray();
        return (bytes[8] & 0xC0) >> 6;
    }

    /// <summary>
    /// 检查 Guid 是否为空
    /// </summary>
    /// <param name="guid">要检查的 Guid</param>
    /// <returns>是否为空 Guid</returns>
    public static bool IsEmpty(Guid guid)
    {
        return guid == Guid.Empty;
    }

    /// <summary>
    /// 检查 Guid 是否不为空
    /// </summary>
    /// <param name="guid">要检查的 Guid</param>
    /// <returns>是否不为空 Guid</returns>
    public static bool IsNotEmpty(Guid guid)
    {
        return guid != Guid.Empty;
    }

    /// <summary>
    /// 生成指定数量的 Guid 列表
    /// </summary>
    /// <param name="count">要生成的数量</param>
    /// <param name="useCrypto">是否使用加密安全的随机数</param>
    /// <returns>Guid 列表</returns>
    /// <exception cref="ArgumentException">数量小于0时抛出</exception>
    public static List<Guid> GenerateMultiple(int count, bool useCrypto = false)
    {
        if (count < 0)
        {
            throw new ArgumentException("数量不能小于0", nameof(count));
        }
        
        var guids = new List<Guid>(count);
        
        for (var i = 0; i < count; i++)
        {
            guids.Add(useCrypto ? NewCryptoGuid() : NewGuid());
        }
        
        return guids;
    }

    /// <summary>
    /// 比较两个 Guid 是否相等
    /// </summary>
    /// <param name="guid1">第一个 Guid</param>
    /// <param name="guid2">第二个 Guid</param>
    /// <returns>是否相等</returns>
    public static bool AreEqual(Guid guid1, Guid guid2)
    {
        return guid1.Equals(guid2);
    }

    /// <summary>
    /// 获取 Guid 的哈希码
    /// </summary>
    /// <param name="guid">要获取哈希码的 Guid</param>
    /// <returns>哈希码</returns>
    public static int GetHashCode(Guid guid)
    {
        return guid.GetHashCode();
    }

    /// <summary>
    /// 将 Guid 转换为 Base64 字符串
    /// </summary>
    /// <param name="guid">要转换的 Guid</param>
    /// <returns>Base64 字符串</returns>
    public static string ToBase64String(Guid guid)
    {
        return Convert.ToBase64String(guid.ToByteArray());
    }

    /// <summary>
    /// 从 Base64 字符串创建 Guid
    /// </summary>
    /// <param name="base64String">Base64 字符串</param>
    /// <returns>创建的 Guid</returns>
    /// <exception cref="ArgumentException">Base64 字符串无效时抛出</exception>
    public static Guid FromBase64String(string base64String)
    {
        ArgumentNullException.ThrowIfNull(base64String);
        
        try
        {
            var bytes = Convert.FromBase64String(base64String);
            return FromByteArray(bytes);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("无效的 Base64 字符串", nameof(base64String), ex);
        }
    }

    /// <summary>
    /// 获取所有支持的 Guid 格式示例
    /// </summary>
    /// <param name="guid">示例 Guid，如果为空则使用新生成的 Guid</param>
    /// <returns>格式示例字典</returns>
    public static Dictionary<string, string> GetFormatExamples(Guid? guid = null)
    {
        var exampleGuid = guid ?? NewGuid();
        
        return new Dictionary<string, string>
        {
            { "N", exampleGuid.ToString("N") },
            { "D", exampleGuid.ToString("D") },
            { "B", exampleGuid.ToString("B") },
            { "P", exampleGuid.ToString("P") },
            { "X", exampleGuid.ToString("X") },
            { "Base64", ToBase64String(exampleGuid) }
        };
    }
}

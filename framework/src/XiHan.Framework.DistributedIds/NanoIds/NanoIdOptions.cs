#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NanoIdOptions
// Guid:b5d9b6c1-7e2a-4f9c-8d7e-6e2a9f1c8d7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/15 10:30:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.NanoIds;

/// <summary>
/// NanoID生成器配置选项
/// </summary>
public class NanoIdOptions
{
    /// <summary>
    /// 默认字符集：URL安全字符集(A-Z, a-z, 0-9)
    /// </summary>
    public const string DefaultAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// 数字字符集(0-9)
    /// </summary>
    public const string NumbersAlphabet = "0123456789";

    /// <summary>
    /// 小写字母字符集(a-z)
    /// </summary>
    public const string LowercaseAlphabet = "abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// 大写字母字符集(A-Z)
    /// </summary>
    public const string UppercaseAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// URL安全字符集(a-z, A-Z, 0-9, _, -)
    /// </summary>
    public const string UrlSafeAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";

    /// <summary>
    /// 安全字符集(无相似字符如: 1, l, I, 0, O, o, u, v, 5, S, s, 2, Z)
    /// </summary>
    public const string SafeAlphabet = "346789ABCDEFGHJKLMNPQRTUWXYabcdefghijkmnpqrtwxyz";

    /// <summary>
    /// 十六进制字符集(0-9, a-f)
    /// </summary>
    public const string HexAlphabet = "0123456789abcdef";

    /// <summary>
    /// 默认开始时间(2020年1月1日)
    /// </summary>
    public static readonly DateTime DefaultStartTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // 字符集
    private string _alphabet = DefaultAlphabet;

    // ID长度
    private int _size = 21;

    // 开始时间
    private DateTime _startTime = DefaultStartTime;

    // 时间戳类型
    private TimestampTypes _timestampType = TimestampTypes.Milliseconds;

    /// <summary>
    /// ID长度
    /// 默认值为21，范围：1-128
    /// </summary>
    public int Size
    {
        get => _size;
        set
        {
            if (value is < 1 or > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ID长度必须在1到128之间");
            }
            _size = value;
        }
    }

    /// <summary>
    /// 字符集
    /// 默认为URL安全字符集(A-Z, a-z, 0-9)
    /// </summary>
    public string Alphabet
    {
        get => _alphabet;
        set
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
            {
                throw new ArgumentException("字符集长度必须至少为2个字符", nameof(value));
            }

            if (value.Distinct().Count() != value.Length)
            {
                throw new ArgumentException("字符集必须包含唯一字符", nameof(value));
            }

            _alphabet = value;
        }
    }

    /// <summary>
    /// 开始时间
    /// 默认为2020年1月1日
    /// </summary>
    public DateTime StartTime
    {
        get => _startTime;
        set => _startTime = value;
    }

    /// <summary>
    /// 时间戳类型
    /// 默认为毫秒级
    /// </summary>
    public TimestampTypes TimestampType
    {
        get => _timestampType;
        set => _timestampType = value;
    }

    /// <summary>
    /// 创建只包含数字的配置
    /// </summary>
    /// <param name="size">长度(默认为10)</param>
    /// <returns>配置对象</returns>
    public static NanoIdOptions OnlyNumbers(int size = 10)
    {
        return new NanoIdOptions
        {
            Size = size,
            Alphabet = NumbersAlphabet
        };
    }

    /// <summary>
    /// 创建只包含小写字母的配置
    /// </summary>
    /// <param name="size">长度(默认为16)</param>
    /// <returns>配置对象</returns>
    public static NanoIdOptions OnlyLowercase(int size = 16)
    {
        return new NanoIdOptions
        {
            Size = size,
            Alphabet = LowercaseAlphabet
        };
    }

    /// <summary>
    /// 创建只包含大写字母的配置
    /// </summary>
    /// <param name="size">长度(默认为16)</param>
    /// <returns>配置对象</returns>
    public static NanoIdOptions OnlyUppercase(int size = 16)
    {
        return new NanoIdOptions
        {
            Size = size,
            Alphabet = UppercaseAlphabet
        };
    }

    /// <summary>
    /// 创建URL安全的配置
    /// </summary>
    /// <param name="size">长度(默认为21)</param>
    /// <returns>配置对象</returns>
    public static NanoIdOptions UrlSafe(int size = 21)
    {
        return new NanoIdOptions
        {
            Size = size,
            Alphabet = UrlSafeAlphabet
        };
    }

    /// <summary>
    /// 创建安全字符集的配置(无相似字符如：1/I/l, 0/O/o 等)
    /// </summary>
    /// <param name="size">长度(默认为21)</param>
    /// <returns>配置对象</returns>
    public static NanoIdOptions Safe(int size = 21)
    {
        return new NanoIdOptions
        {
            Size = size,
            Alphabet = SafeAlphabet
        };
    }

    /// <summary>
    /// 创建十六进制字符集的配置
    /// </summary>
    /// <param name="size">长度(默认为32)</param>
    /// <returns>配置对象</returns>
    public static NanoIdOptions Hex(int size = 32)
    {
        return new NanoIdOptions
        {
            Size = size,
            Alphabet = HexAlphabet
        };
    }
}

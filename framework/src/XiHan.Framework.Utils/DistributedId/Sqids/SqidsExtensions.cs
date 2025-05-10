﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqidsExtensions
// Author:ZhaiFanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/08 17:46:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Maths;

namespace XiHan.Framework.Utils.DistributedId.Sqids;

/// <summary>
/// Sqids扩展方法
/// </summary>
public static class SqidsExtensions
{
    private static readonly SqidsEncoder<Int32Number> _int32NumberEncoder = new();
    private static readonly SqidsEncoder<Int64Number> _int64NumberEncoder = new();
    private static readonly SqidsEncoder<UInt32Number> _uInt32NumberEncoder = new();
    private static readonly SqidsEncoder<UInt64Number> _uInt64NumberEncoder = new();

    /// <summary>
    /// 将整数编码为Sqids格式
    /// </summary>
    /// <param name="number">整数</param>
    /// <returns>编码后的字符串</returns>
    public static string ToSqid(this int number)
    {
        return _int32NumberEncoder.Encode(number);
    }

    /// <summary>
    /// 将多个整数编码为Sqids格式
    /// </summary>
    /// <param name="numbers">整数数组</param>
    /// <returns>编码后的字符串</returns>
    public static string ToSqid(this int[] numbers)
    {
        return _int32NumberEncoder.Encode([.. numbers.Select(n => (Int32Number)n)]);
    }

    /// <summary>
    /// 将长整数编码为Sqids格式
    /// </summary>
    /// <param name="number">长整数</param>
    /// <returns>编码后的字符串</returns>
    public static string ToSqid(this long number)
    {
        return _int64NumberEncoder.Encode(number);
    }

    /// <summary>
    /// 将多个长整数编码为Sqids格式
    /// </summary>
    /// <param name="numbers">长整数数组</param>
    /// <returns>编码后的字符串</returns>
    public static string ToSqid(this long[] numbers)
    {
        return _int64NumberEncoder.Encode([.. numbers.Select(n => (Int64Number)n)]);
    }

    /// <summary>
    /// 将无符号整数编码为Sqids格式
    /// </summary>
    /// <param name="number">无符号整数</param>
    /// <returns>编码后的字符串</returns>
    public static string ToSqid(this uint number)
    {
        return _uInt32NumberEncoder.Encode(number);
    }

    /// <summary>
    /// 将无符号长整数编码为Sqids格式
    /// </summary>
    /// <param name="number">无符号长整数</param>
    /// <returns>编码后的字符串</returns>
    public static string ToSqid(this ulong number)
    {
        return _uInt64NumberEncoder.Encode(number);
    }

    /// <summary>
    /// 将Sqids字符串解码为整数
    /// </summary>
    /// <param name="sqid">Sqids字符串</param>
    /// <returns>解码后的整数</returns>
    public static int FromSqidToInt32(this string sqid)
    {
        var result = _int32NumberEncoder.Decode(sqid);
        return result.Length > 0 ? result[0] : 0;
    }

    /// <summary>
    /// 将Sqids字符串解码为整数数组
    /// </summary>
    /// <param name="sqid">Sqids字符串</param>
    /// <returns>解码后的整数数组</returns>
    public static int[] FromSqidToInt32Array(this string sqid)
    {
        return [.. _int32NumberEncoder.Decode(sqid).Select(n => (int)n)];
    }

    /// <summary>
    /// 将Sqids字符串解码为长整数
    /// </summary>
    /// <param name="sqid">Sqids字符串</param>
    /// <returns>解码后的长整数</returns>
    public static long FromSqidToInt64(this string sqid)
    {
        var result = _int64NumberEncoder.Decode(sqid);
        return result.Length > 0 ? result[0] : 0;
    }

    /// <summary>
    /// 将Sqids字符串解码为长整数数组
    /// </summary>
    /// <param name="sqid">Sqids字符串</param>
    /// <returns>解码后的长整数数组</returns>
    public static long[] FromSqidToInt64Array(this string sqid)
    {
        return [.. _int64NumberEncoder.Decode(sqid).Select(n => (long)n)];
    }

    /// <summary>
    /// 将Sqids字符串解码为无符号整数
    /// </summary>
    /// <param name="sqid">Sqids字符串</param>
    /// <returns>解码后的无符号整数</returns>
    public static uint FromSqidToUInt32(this string sqid)
    {
        var result = _uInt32NumberEncoder.Decode(sqid);
        return result.Length > 0 ? result[0] : 0;
    }

    /// <summary>
    /// 将Sqids字符串解码为无符号长整数
    /// </summary>
    /// <param name="sqid">Sqids字符串</param>
    /// <returns>解码后的无符号长整数</returns>
    public static ulong FromSqidToUInt64(this string sqid)
    {
        var result = _uInt64NumberEncoder.Decode(sqid);
        return result.Length > 0 ? result[0] : 0;
    }
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Base95
// Guid:39bb5fd2-94c8-43da-a4c8-c44331511dac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/23 21:34:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Numerics;
using System.Text;

namespace XiHan.Framework.Utils.Text.Converters;

/// <summary>
/// Base95 编码和解码
/// </summary>
/// <remarks>
/// 主要特点：最紧凑的标准编码之一，可打印字符集，可读性较差，URL 不完全安全
/// 常见用途：密钥/口令生成(可打印)、二维码 / 短信传输、嵌入式系统传输数据、空间压缩极致场景、数据序列化压缩格式
/// </remarks>
public static class Base95
{
    private const int Base = 95;

    // ASCII 可打印字符(从 32 到 126，共 95 个字符)
    // !"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~
    // 0-31 ASCII 控制字符
    // 32-126 ASCII 字符
    // 127-255 扩展 ASCII 字符
    private static readonly char[] Alphabet = [.. Enumerable.Range(32, 95).Select(i => (char)i)]; // ASCII 32–126

    /// <summary>
    /// 编码 byte[] 为 Base95 字符串
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string Encode(byte[] data)
    {
        var value = new BigInteger(data.Concat(new byte[] { 0 }).ToArray());
        if (value == 0)
        {
            return Alphabet[0].ToString();
        }

        var sb = new StringBuilder();
        while (value > 0)
        {
            var rem = (int)(value % Base);
            sb.Insert(0, Alphabet[rem]);
            value /= Base;
        }

        // 前导0处理
        foreach (var b in data)
        {
            if (b == 0)
            {
                sb.Insert(0, Alphabet[0]);
            }
            else
            {
                break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解码 Base95 字符串为 byte[]
    /// </summary>
    /// <param name="encoded"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static byte[] Decode(string encoded)
    {
        BigInteger value = 0;
        foreach (var c in encoded)
        {
            var index = c - 32;
            if (index is < 0 or >= 95)
            {
                throw new ArgumentException($"非法 Base95 字符: {c}");
            }

            value = (value * Base) + index;
        }

        var bytes = value.ToByteArray();
        if (bytes[^1] == 0)
        {
            bytes = [.. bytes.Take(bytes.Length - 1)];
        }

        var leadingZeroCount = encoded.TakeWhile(c => c == Alphabet[0]).Count();
        return [.. Enumerable.Repeat((byte)0, leadingZeroCount), .. bytes.Reverse()];
    }
}

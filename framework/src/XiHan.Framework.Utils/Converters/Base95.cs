#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Base95
// Guid:39bb5fd2-94c8-43da-a4c8-c44331511dac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/23 21:34:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Buffers;
using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base95 编码和解码（ASCII 可打印字符标准）
/// </summary>
/// <remarks>
/// 使用 ASCII 可打印字符（32-126，共 95 个字符）
/// 主要特点：最紧凑的标准编码之一，可打印字符集，可读性较差，URL 不完全安全
/// 常见用途：密钥/口令生成(可打印)、二维码/短信传输、嵌入式系统传输数据、空间压缩极致场景、数据序列化压缩格式
/// 标准参考：ASCII 标准（无特定 RFC）
/// </remarks>
public static class Base95
{
    private const int Base = 95;

    // ASCII 可打印字符(从 32 到 126，共 95 个字符)
    // !"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~
    // 0-31 ASCII 控制字符
    // 32-126 ASCII 字符
    // 127-255 扩展 ASCII 字符
    private static readonly char[] Alphabet;

    static Base95()
    {
        // 优化：使用直接数组初始化，避免 LINQ
        Alphabet = new char[95];
        for (var i = 0; i < 95; i++)
        {
            Alphabet[i] = (char)(i + 32);
        }
    }

    /// <summary>
    /// 编码 byte[] 为 Base95 字符串
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string Encode(byte[] data)
    {
        // 使用 ArrayPool 租用缓冲区
        var tempBuffer = ArrayPool<byte>.Shared.Rent(data.Length + 1);
        try
        {
            // 复制数据并添加0字节防止负数
            data.CopyTo(tempBuffer.AsSpan());
            tempBuffer[data.Length] = 0;

            var value = new BigInteger(tempBuffer.AsSpan(0, data.Length + 1));

            if (value == 0)
            {
                return Alphabet[0].ToString();
            }

            // 计算前导零的数量
            var leadingZeroCount = 0;
            foreach (var b in data)
            {
                if (b == 0)
                {
                    leadingZeroCount++;
                }
                else
                {
                    break;
                }
            }

            // 使用 stackalloc 存储结果字符
            var maxChars = (int)Math.Ceiling(data.Length * 1.23) + leadingZeroCount; // Base95 扩展率约1.23倍
            var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
            var index = 0;

            // 正向构建（后面会反转）
            while (value > 0)
            {
                var rem = (int)(value % Base);
                resultSpan[index++] = Alphabet[rem];
                value /= Base;
            }

            // 添加前导零
            for (var i = 0; i < leadingZeroCount; i++)
            {
                resultSpan[index++] = Alphabet[0];
            }

            // 反转结果
            resultSpan[..index].Reverse();
            return new string(resultSpan[..index]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
        }
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

        // 移除末尾补零
        var bytesLength = bytes.Length;
        if (bytesLength > 0 && bytes[^1] == 0)
        {
            bytesLength--;
        }

        // 计算前导零的数量
        var leadingZeroCount = 0;
        foreach (var c in encoded)
        {
            if (c == Alphabet[0])
            {
                leadingZeroCount++;
            }
            else
            {
                break;
            }
        }

        // 构建最终结果
        var result = new byte[leadingZeroCount + bytesLength];

        // 反转并复制字节（跳过末尾的0）
        for (var i = 0; i < bytesLength; i++)
        {
            result[leadingZeroCount + i] = bytes[bytesLength - 1 - i];
        }

        return result;
    }
}

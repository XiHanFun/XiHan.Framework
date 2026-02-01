#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Base62
// Guid:200dd36c-2d20-40f3-be57-fa44648984f5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/23 20:43:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Buffers;
using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base62 编码解码（0-9A-Za-z 标准）
/// </summary>
/// <remarks>
/// 使用标准 Base62 字符集（0-9、A-Z、a-z，共 62 个字符）
/// 主要特点：包含数字+大小写字母，较紧凑，编码长度更短，URL 安全
/// 常见用途：短链接、邀请码、用户唯一标识编码、YouTube 视频 ID 等
/// 标准参考：数学进制转换标准（无特定 RFC）
/// </remarks>
public static class Base62
{
    // 字符表：62 个字符(0-9A-Za-z)
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static readonly Dictionary<char, int> CharMap = [];

    static Base62()
    {
        for (var i = 0; i < Base62Alphabet.Length; i++)
        {
            CharMap[Base62Alphabet[i]] = i;
        }
    }

    /// <summary>
    /// 编码 byte 数组为 Base62 字符串
    /// </summary>
    public static string Encode(byte[] input)
    {
        // 使用 ArrayPool 租用缓冲区
        var tempBuffer = ArrayPool<byte>.Shared.Rent(input.Length + 1);
        try
        {
            // 复制数据并添加0字节防止负数
            input.CopyTo(tempBuffer.AsSpan());
            tempBuffer[input.Length] = 0;

            var intData = new BigInteger(tempBuffer.AsSpan(0, input.Length + 1));

            if (intData == 0)
            {
                return "0";
            }

            // 使用 stackalloc 存储结果字符（预估最大长度）
            var maxChars = (int)Math.Ceiling(input.Length * 1.37); // Base62 扩展率约1.37倍
            var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
            var index = 0;

            // 正向构建（后面会反转）
            while (intData > 0)
            {
                var remainder = (int)(intData % 62);
                resultSpan[index++] = Base62Alphabet[remainder];
                intData /= 62;
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
    /// 解码 Base62 字符串为 byte 数组
    /// </summary>
    public static byte[] Decode(string input)
    {
        BigInteger intData = 0;
        foreach (var c in input)
        {
            intData = (intData * 62) + CharMap[c];
        }

        var bytes = intData.ToByteArray();

        // 移除末尾补零
        if (bytes.Length > 0 && bytes[^1] == 0)
        {
            var result = new byte[bytes.Length - 1];
            Array.Copy(bytes, result, result.Length);
            return result;
        }

        return bytes;
    }

    /// <summary>
    /// 编码 long 值为 Base62 字符串
    /// </summary>
    public static string EncodeLong(long input)
    {
        if (input == 0)
        {
            return "0";
        }

        // 使用 stackalloc 存储结果（long 最多需要11个字符）
        Span<char> buffer = stackalloc char[11];
        var index = 0;

        // 正向构建（后面会反转）
        while (input > 0)
        {
            buffer[index++] = Base62Alphabet[(int)(input % 62)];
            input /= 62;
        }

        // 反转结果
        buffer[..index].Reverse();
        return new string(buffer[..index]);
    }

    /// <summary>
    /// 解码 Base62 字符串为 long 值
    /// </summary>
    public static long DecodeLong(string input)
    {
        long result = 0;
        foreach (var c in input)
        {
            result = (result * 62) + CharMap[c];
        }
        return result;
    }
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Base62
// Guid:200dd36c-2d20-40f3-be57-fa44648984f5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/23 20:43:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Numerics;
using System.Text;

namespace XiHan.Framework.Utils.Text.Converters;

/// <summary>
/// Base62 编码解码
/// </summary>
/// <remarks>
/// 主要特点：包含数字+大小写字母，较紧凑，编码长度更短
/// 常见用途：短链接、邀请码、用户ID编码等
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
        var intData = new BigInteger(input.Concat(new byte[] { 0 }).ToArray()); // 防止负数
        var result = new StringBuilder();
        while (intData > 0)
        {
            var remainder = (int)(intData % 62);
            intData /= 62;
            result.Insert(0, Base62Alphabet[remainder]);
        }
        return result.ToString();
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
        if (bytes[^1] == 0)
        {
            bytes = [.. bytes.Take(bytes.Length - 1)]; // 移除补零
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

        var result = new StringBuilder();
        while (input > 0)
        {
            result.Insert(0, Base62Alphabet[(int)(input % 62)]);
            input /= 62;
        }
        return result.ToString();
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

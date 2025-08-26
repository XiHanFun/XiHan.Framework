#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Base58
// Guid:848f3392-8b73-49e6-859a-b3ec8b246d6c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/23 20:45:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Numerics;
using System.Text;

namespace XiHan.Framework.Utils.Text.Converters;

/// <summary>
/// Base58 编码和解码
/// </summary>
/// <remarks>
/// 主要特点：排除易混字符(0/O/I/l)，高可读性，编码长度较短
/// 常见用途：比特币地址、钱包ID、邀请码
/// </remarks>
public static class Base58
{
    // Bitcoin Base58 字符表(不含 0, O, I, l 等易混淆字符)
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    private static readonly int[] Indexes = new int[128];

    static Base58()
    {
        for (var i = 0; i < Indexes.Length; i++)
        {
            Indexes[i] = -1;
        }

        for (var i = 0; i < Alphabet.Length; i++)
        {
            Indexes[Alphabet[i]] = i;
        }
    }

    /// <summary>
    /// 编码 byte 数组为 Base58 字符串
    /// </summary>
    public static string Encode(byte[] input)
    {
        // 转换为大整数
        var intData = new BigInteger(input.Concat(new byte[] { 0 }).ToArray()); // 防止负数
        var result = new StringBuilder();

        // 进行 Base58 编码
        while (intData > 0)
        {
            var remainder = (int)(intData % 58);
            intData /= 58;
            result.Insert(0, Alphabet[remainder]);
        }

        // 处理前导0(Base58 用 '1' 表示)
        foreach (var b in input)
        {
            if (b == 0)
            {
                result.Insert(0, '1');
            }
            else
            {
                break;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 解码 Base58 字符串为 byte 数组
    /// </summary>
    public static byte[] Decode(string input)
    {
        // 计算 Base58 转为大整数
        BigInteger intData = 0;
        foreach (var c in input)
        {
            var digit = Indexes[c];
            if (digit < 0)
            {
                throw new FormatException($"Invalid Base58 character `{c}`");
            }

            intData = intData * 58 + digit;
        }

        // 转换成 byte[]
        var bytes = intData.ToByteArray();
        if (bytes[^1] == 0)
        {
            bytes = [.. bytes.Take(bytes.Length - 1)]; // 移除补零
        }

        // 处理前导 '1'(即 Base58 中的0)
        var leadingZeros = input.TakeWhile(c => c == '1').Count();
        return [.. Enumerable.Repeat((byte)0, leadingZeros), .. bytes.Reverse()];
    }
}

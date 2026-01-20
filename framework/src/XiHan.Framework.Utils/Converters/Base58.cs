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

using System.Buffers;
using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base58 编码和解码（Bitcoin Base58 标准）
/// </summary>
/// <remarks>
/// 符合 Bitcoin Base58 编码标准
/// 主要特点：排除易混字符(0/O/I/l)，高可读性，编码长度较短
/// 常见用途：比特币地址、钱包唯一标识、邀请码、区块链应用
/// 标准参考：https://en.bitcoin.it/wiki/Base58Check_encoding
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
        // 使用 ArrayPool 租用缓冲区
        var tempBuffer = ArrayPool<byte>.Shared.Rent(input.Length + 1);
        try
        {
            // 复制数据并添加0字节防止负数
            input.CopyTo(tempBuffer.AsSpan());
            tempBuffer[input.Length] = 0;

            var intData = new BigInteger(tempBuffer.AsSpan(0, input.Length + 1));

            // 计算前导零的数量
            var leadingZeroCount = 0;
            foreach (var b in input)
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

            if (intData == 0)
            {
                return new string('1', Math.Max(1, leadingZeroCount));
            }

            // 使用 stackalloc 存储结果字符
            var maxChars = (int)Math.Ceiling(input.Length * 1.38) + leadingZeroCount; // Base58 扩展率约1.38倍
            var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
            var index = 0;

            // 正向构建（后面会反转）
            while (intData > 0)
            {
                var remainder = (int)(intData % 58);
                resultSpan[index++] = Alphabet[remainder];
                intData /= 58;
            }

            // 添加前导 '1'（Base58 中表示 0）
            for (var i = 0; i < leadingZeroCount; i++)
            {
                resultSpan[index++] = '1';
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

            intData = (intData * 58) + digit;
        }

        // 转换成 byte[]
        var bytes = intData.ToByteArray();

        // 移除末尾补零
        var bytesLength = bytes.Length;
        if (bytesLength > 0 && bytes[^1] == 0)
        {
            bytesLength--;
        }

        // 计算前导 '1' 的数量（Base58 中的0）
        var leadingZeroCount = 0;
        foreach (var c in input)
        {
            if (c == '1')
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

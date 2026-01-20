#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Base36
// Guid:afdafa23-4475-4347-a4a0-4923da87148f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/23 21:33:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Buffers;
using System.Numerics;

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base36 编码和解码（0-9A-Z 标准）
/// </summary>
/// <remarks>
/// 使用标准 Base36 字符集（0-9 和 A-Z，共 36 个字符）
/// 主要特点：比 Base16 更短，但不如 Base62/Base64 紧凑，人类可识别，适合数字与字母组合使用，不包含特殊符号，适合用户手输
/// 常见用途：邀请码、用户标识、订单号、编号编码、短链接唯一标识、数字压缩显示等
/// 标准参考：数学进制转换标准（无特定 RFC）
/// </remarks>
public static class Base36
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 编码 byte[] 为 Base36 字符串
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
            var maxChars = (int)Math.Ceiling(data.Length * 1.4) + leadingZeroCount; // Base36 扩展率约1.4倍
            var resultSpan = maxChars <= 128 ? stackalloc char[maxChars] : new char[maxChars];
            var index = 0;

            // 正向构建（后面会反转）
            while (value > 0)
            {
                var rem = (int)(value % 36);
                resultSpan[index++] = Alphabet[rem];
                value /= 36;
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
    /// 解码 Base36 字符串为 byte[]
    /// </summary>
    /// <param name="encoded"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static byte[] Decode(string encoded)
    {
        BigInteger value = 0;
        foreach (var c in encoded)
        {
            var index = Alphabet.IndexOf(c);
            if (index == -1)
            {
                throw new ArgumentException($"非法 Base36 字符: {c}");
            }

            value = (value * 36) + index;
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

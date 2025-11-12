#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Base32
// Guid:fbfc91d0-4518-482a-8516-e68be65370a7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/23 20:42:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Text.Converters;

/// <summary>
/// Base32 编码解码
/// </summary>
/// <remarks>
/// 主要特点：不区分大小写、支持错误检测，编码长度较长
/// 常见用途：TOTP(谷歌验证码)、DNS编码、二维码等
/// </remarks>
public static class Base32
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// 二进制转换为 Base32
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string Encode(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new(((input.Length * 8) + 4) / 5);

        var bitCount = 0;
        var accumulatedBits = 0;

        foreach (var currentByte in input)
        {
            accumulatedBits |= currentByte << bitCount;
            bitCount += 8;
            while (bitCount >= 5)
            {
                const int Mask = 0x1f;
                var currentBase32Value = accumulatedBits & Mask;
                sb.Append(Base32Alphabet[currentBase32Value]);
                accumulatedBits >>= 5;
                bitCount -= 5;
            }
        }

        if (bitCount <= 0)
        {
            return sb.ToString();
        }

        {
            const int Mask = 0x1f;
            var currentBase32Value = accumulatedBits & Mask;
            sb.Append(Base32Alphabet[currentBase32Value]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Base32 转换为二进制
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static byte[] Decode(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
        {
            return [];
        }

        input = input.TrimEnd('=');

        var byteCount = input.Length * 5 / 8;
        var buffer = new byte[byteCount];

        var bitCount = 0;
        var accumulatedBits = 0;
        var bufferIndex = 0;

        foreach (var currentChar in input)
        {
            var currentCharValue = Base32Alphabet.IndexOf(currentChar);
            if (currentCharValue is < 0 or > 31)
            {
                throw new ArgumentException("Invalid character in Base32 string.");
            }

            accumulatedBits |= currentCharValue << bitCount;
            bitCount += 5;

            if (bitCount < 8)
            {
                continue;
            }

            const int Mask = 0xff;
            var currentByteValue = accumulatedBits & Mask;
            buffer[bufferIndex++] = (byte)currentByteValue;
            accumulatedBits >>= 8;
            bitCount -= 8;
        }

        return buffer;
    }
}

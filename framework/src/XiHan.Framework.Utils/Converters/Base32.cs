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

namespace XiHan.Framework.Utils.Converters;

/// <summary>
/// Base32 编码解码（RFC 4648 标准）
/// </summary>
/// <remarks>
/// 符合 RFC 4648 标准，使用 Big-Endian 字节序
/// 主要特点：不区分大小写、支持错误检测，编码长度较长
/// 常见用途：TOTP(谷歌验证码)、HOTP、DNS 编码、二维码等
/// 标准参考：https://tools.ietf.org/html/rfc4648
/// </remarks>
public static class Base32
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// 二进制转换为 Base32（RFC 4648 标准 - Big-Endian 字节序）
    /// </summary>
    /// <param name="input">待编码的字节数组</param>
    /// <returns>Base32 编码的字符串</returns>
    /// <exception cref="ArgumentNullException">当输入为 null 时抛出</exception>
    public static string Encode(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder(((input.Length * 8) + 4) / 5);

        // 按照 5 字节一组处理（5 字节 = 40 位 = 8 个 Base32 字符）
        for (var i = 0; i < input.Length; i += 5)
        {
            var byteCount = Math.Min(5, input.Length - i);
            ulong buffer = 0;

            // Big-Endian 处理：从高位到低位
            for (var j = 0; j < byteCount; j++)
            {
                buffer = (buffer << 8) | input[i + j];
            }

            var bitCount = byteCount * 8;
            buffer <<= (5 - byteCount) * 8;

            // 提取每 5 位作为一个 Base32 字符索引
            for (var j = 0; j < (bitCount + 4) / 5; j++)
            {
                var index = (int)((buffer >> (35 - (j * 5))) & 0x1F);
                result.Append(Base32Alphabet[index]);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Base32 转换为二进制（RFC 4648 标准 - Big-Endian 字节序）
    /// </summary>
    /// <param name="input">Base32 编码的字符串</param>
    /// <returns>解码后的字节数组</returns>
    /// <exception cref="ArgumentNullException">当输入为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当输入包含非法字符时抛出</exception>
    public static byte[] Decode(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
        {
            return [];
        }

        // 移除空白字符和填充符，转为大写
        input = input.Trim().TrimEnd('=').ToUpperInvariant();

        if (input.Length == 0)
        {
            return [];
        }

        var result = new List<byte>();

        // 按照 8 个字符一组处理（8 个 Base32 字符 = 40 位 = 5 字节）
        for (var i = 0; i < input.Length; i += 8)
        {
            var chunkLength = Math.Min(8, input.Length - i);
            ulong buffer = 0;

            // Big-Endian 处理：从高位到低位
            for (var j = 0; j < chunkLength; j++)
            {
                var value = Base32Alphabet.IndexOf(input[i + j]);
                if (value < 0)
                {
                    throw new ArgumentException($"非法的 Base32 字符: {input[i + j]}", nameof(input));
                }

                buffer = (buffer << 5) | (uint)value;
            }

            var bitCount = chunkLength * 5;
            buffer <<= (8 - chunkLength) * 5;

            // 提取每 8 位作为一个字节
            for (var j = 0; j < bitCount / 8; j++)
            {
                result.Add((byte)((buffer >> (32 - (j * 8))) & 0xFF));
            }
        }

        return [.. result];
    }
}

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

using System.Numerics;
using System.Text;

namespace XiHan.Framework.Utils.Text.Converters;

/// <summary>
/// Base36 编码和解码
/// </summary>
/// <remarks>
/// 主要特点：比 Base16 更短，但不如 Base62/Base64 紧凑，人类可识别，适合数字与字母组合使用，不包含特殊符号，适合用户手输
/// 常见用途：邀请码、用户标识、订单号、编号编码、短链接 ID、数字压缩显示等
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
        var value = new BigInteger(data.Concat(new byte[] { 0 }).ToArray());
        if (value == 0)
        {
            return Alphabet[0].ToString();
        }

        var sb = new StringBuilder();
        while (value > 0)
        {
            var rem = (int)(value % 36);
            sb.Insert(0, Alphabet[rem]);
            value /= 36;
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

            value = value * 36 + index;
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

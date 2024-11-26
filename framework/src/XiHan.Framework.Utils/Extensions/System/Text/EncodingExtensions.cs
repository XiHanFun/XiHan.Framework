#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EncodingExtensions
// Guid:5ac68b45-83e3-4ab6-8401-a0ec77090db5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 1:40:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace XiHan.Framework.Utils.Extensions.System.Text;

/// <summary>
/// 编码扩展方法
/// </summary>
public static partial class EncodingExtensions
{
    /// <summary>
    /// 对字符串进行 Base32 编码
    /// </summary>
    /// <param name="data">待编码的字符串</param>
    /// <returns>编码后的字符串</returns>
    public static string Base32Encode(this string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        return Base32.ToBase32String(bytes);
    }

    /// <summary>
    /// 对 Base32 编码的字符串进行解码
    /// </summary>
    /// <param name="data">待解码的字符串</param>
    /// <returns>解码后的字符串</returns>
    public static string Base32Decode(this string data)
    {
        var bytes = Base32.FromBase32String(data);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// 对字符串进行 Base64 编码
    /// </summary>
    /// <param name="data">待编码的字符串</param>
    /// <returns>编码后的字符串</returns>
    public static string Base64Encode(this string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 对 Base64 编码的字符串进行解码
    /// </summary>
    /// <param name="data">待解码的字符串</param>
    /// <returns>解码后的字符串</returns>
    public static string Base64Decode(this string data)
    {
        var bytes = Convert.FromBase64String(data);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// 对字符串进行 HTML 编码
    /// </summary>
    /// <param name="data">待编码的字符串</param>
    /// <returns>编码后的字符串</returns>
    public static string HtmlEncode(this string data)
    {
        return HttpUtility.HtmlEncode(data);
    }

    /// <summary>
    /// 对 HTML 编码的字符串进行解码
    /// </summary>
    /// <param name="data">待解码的字符串</param>
    /// <returns>解码后的字符串</returns>
    public static string HtmlDecode(this string data)
    {
        return HttpUtility.HtmlDecode(data);
    }

    /// <summary>
    /// 对字符串进行 URL 编码
    /// </summary>
    /// <param name="data">待编码的字符串</param>
    /// <returns>编码后的字符串</returns>
    public static string UrlEncode(this string data)
    {
        return WebUtility.UrlEncode(data);
    }

    /// <summary>
    /// 对 URL 编码的字符串进行解码
    /// </summary>
    /// <param name="data">待解码的字符串</param>
    /// <returns>解码后的字符串</returns>
    public static string UrlDecode(this string data)
    {
        return WebUtility.UrlDecode(data);
    }

    /// <summary>
    /// 将字符串转为 Unicode 编码
    /// </summary>
    /// <param name="data">待转码的字符串</param>
    /// <returns>转码后的字符串</returns>
    public static string UnicodeEncode(this string data)
    {
        StringBuilder sb = new();
        foreach (var t in data)
        {
            _ = sb.Append($@"\u{(int)t:x4}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 将 Unicode 编码转换为原始的字符串
    /// </summary>
    /// <param name="data">待解码的字符串</param>
    /// <returns>解码后的字符串</returns>
    public static string UnicodeDecode(this string data)
    {
        return UnicodeRegex().Replace(data, match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber)).ToString());
    }

    /// <summary>
    /// 将字符串转化为二进制
    /// </summary>
    /// <param name="data">待转换的字符串</param>
    /// <returns>转换后的二进制数组</returns>
    public static byte[] BinaryEncode(this string data)
    {
        return Encoding.UTF8.GetBytes(data);
    }

    /// <summary>
    /// 将二进制数据转化为字符串
    /// </summary>
    /// <param name="data">待转换的二进制数组</param>
    /// <returns>转换后的字符串</returns>
    public static string BinaryDecode(this byte[] data)
    {
        return Encoding.UTF8.GetString(data);
    }

    /// <summary>
    /// 将字符串转化为文件流
    /// </summary>
    /// <param name="data">待转换的字符串</param>
    /// <returns>转换后的二进制数组</returns>
    public static Stream ToStream(this string data)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(data));
    }

    [GeneratedRegex(@"\\u([0-9A-Za-z]{4})")]
    private static partial Regex UnicodeRegex();
}

/// <summary>
/// 手写 Base32 转换
/// </summary>
public static class Base32
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// 二进制转换为 Base32
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string ToBase32String(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new(((bytes.Length * 8) + 4) / 5);

        var bitCount = 0;
        var accumulatedBits = 0;

        foreach (var currentByte in bytes)
        {
            accumulatedBits |= currentByte << bitCount;
            bitCount += 8;
            while (bitCount >= 5)
            {
                const int mask = 0x1f;
                var currentBase32Value = accumulatedBits & mask;
                _ = sb.Append(Base32Alphabet[currentBase32Value]);
                accumulatedBits >>= 5;
                bitCount -= 5;
            }
        }

        if (bitCount <= 0)
        {
            return sb.ToString();
        }

        {
            const int mask = 0x1f;
            var currentBase32Value = accumulatedBits & mask;
            _ = sb.Append(Base32Alphabet[currentBase32Value]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Base32 转换为二进制
    /// </summary>
    /// <param name="base32String"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static byte[] FromBase32String(string base32String)
    {
        ArgumentNullException.ThrowIfNull(base32String);

        if (base32String.Length == 0)
        {
            return [];
        }

        base32String = base32String.TrimEnd('=');

        var byteCount = base32String.Length * 5 / 8;
        var buffer = new byte[byteCount];

        var bitCount = 0;
        var accumulatedBits = 0;
        var bufferIndex = 0;
        foreach (var currentCharValue in base32String.Select(currentChar => Base32Alphabet.IndexOf(currentChar)))
        {
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

            const int mask = 0xff;
            var currentByteValue = accumulatedBits & mask;
            buffer[bufferIndex++] = (byte)currentByteValue;
            accumulatedBits >>= 8;
            bitCount -= 8;
        }

        return buffer;
    }
}
#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
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
using System.Web;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Text.Converters;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 编码扩展方法
/// </summary>
public static class EncodingExtensions
{
    /// <summary>
    /// 对字符串进行 Base32 编码
    /// </summary>
    /// <param name="data">待编码的字符串</param>
    /// <returns>编码后的字符串</returns>
    public static string Base32Encode(this string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        return Base32.Encode(bytes);
    }

    /// <summary>
    /// 对 Base32 编码的字符串进行解码
    /// </summary>
    /// <param name="data">待解码的字符串</param>
    /// <returns>解码后的字符串</returns>
    public static string Base32Decode(this string data)
    {
        var bytes = Base32.Decode(data);
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
        var regex = RegexHelper.UnicodeRegex();
        return regex.Replace(data, match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber)).ToString());
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

    /// <summary>
    /// 将字符串转换为二进制表示
    /// </summary>
    public static string FromStringToBinary(this string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var result = new StringBuilder();

        foreach (var b in bytes)
        {
            result.Append(Convert.ToString(b, 8));
        }

        return result.ToString();
    }

    /// <summary>
    /// 将二进制表示转换为字符串
    /// </summary>
    public static string FromBinaryToString(this string binary)
    {
        try
        {
            var byteChunks = new List<byte>();

            for (var i = 0; i < binary.Length; i += 3)
            {
                if (i + 3 <= binary.Length)
                {
                    var chunk = binary.Substring(i, 3);
                    if (byte.TryParse(chunk, out var byteValue))
                    {
                        byteChunks.Add(byteValue);
                    }
                }
            }

            return Encoding.UTF8.GetString([.. byteChunks]);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 对字符串中的特殊字符进行转义
    /// </summary>
    /// <param name="data">待转义的字符串</param>
    /// <returns>转义后的字符串</returns>
    public static string EscapeString(this string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }

        return data
            .Replace("\\", "\\\\")  // 反斜杠必须最先处理
            .Replace("\"", "\\\"")  // 双引号
            .Replace("'", "\\'")    // 单引号
            .Replace("\n", "\\n")   // 换行符
            .Replace("\r", "\\r")   // 回车符
            .Replace("\t", "\\t")   // 制表符
            .Replace("\b", "\\b")   // 退格符
            .Replace("\f", "\\f")   // 换页符
            .Replace("\a", "\\a")   // 响铃符
            .Replace("\v", "\\v")   // 垂直制表符
            .Replace("\0", "\\0");  // 空字符
    }

    /// <summary>
    /// 对转义字符串进行反转义
    /// </summary>
    /// <param name="data">待反转义的字符串</param>
    /// <returns>反转义后的字符串</returns>
    public static string UnescapeString(this string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }

        return data
            .Replace("\\\"", "\"")  // 双引号
            .Replace("\\'", "'")    // 单引号
            .Replace("\\n", "\n")   // 换行符
            .Replace("\\r", "\r")   // 回车符
            .Replace("\\t", "\t")   // 制表符
            .Replace("\\b", "\b")   // 退格符
            .Replace("\\f", "\f")   // 换页符
            .Replace("\\a", "\a")   // 响铃符
            .Replace("\\v", "\v")   // 垂直制表符
            .Replace("\\0", "\0")   // 空字符
            .Replace("\\\\", "\\"); // 反斜杠必须最后处理
    }

    /// <summary>
    /// 对字符串中的特殊字符进行 C# 风格转义
    /// </summary>
    /// <param name="data">待转义的字符串</param>
    /// <returns>转义后的字符串</returns>
    public static string EscapeForCSharp(this string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }

        var sb = new StringBuilder();
        foreach (var c in data)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;

                case '"':
                    sb.Append("\\\"");
                    break;

                case '\'':
                    sb.Append("\\'");
                    break;

                case '\n':
                    sb.Append("\\n");
                    break;

                case '\r':
                    sb.Append("\\r");
                    break;

                case '\t':
                    sb.Append("\\t");
                    break;

                case '\b':
                    sb.Append("\\b");
                    break;

                case '\f':
                    sb.Append("\\f");
                    break;

                case '\a':
                    sb.Append("\\a");
                    break;

                case '\v':
                    sb.Append("\\v");
                    break;

                case '\0':
                    sb.Append("\\0");
                    break;

                default:
                    // 处理其他控制字符
                    if (char.IsControl(c))
                    {
                        sb.Append($"\\u{(int)c:x4}");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 对字符串中的特殊字符进行 JSON 风格转义
    /// </summary>
    /// <param name="data">待转义的字符串</param>
    /// <returns>转义后的字符串</returns>
    public static string EscapeForJson(this string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return data;
        }

        var sb = new StringBuilder();
        foreach (var c in data)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;

                case '\\':
                    sb.Append("\\\\");
                    break;

                case '/':
                    sb.Append("\\/");
                    break;

                case '\b':
                    sb.Append("\\b");
                    break;

                case '\f':
                    sb.Append("\\f");
                    break;

                case '\n':
                    sb.Append("\\n");
                    break;

                case '\r':
                    sb.Append("\\r");
                    break;

                case '\t':
                    sb.Append("\\t");
                    break;

                default:
                    // 处理 Unicode 控制字符
                    if (char.IsControl(c))
                    {
                        sb.Append($"\\u{(int)c:x4}");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }
}
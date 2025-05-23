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
using XiHan.Framework.Utils.System.Converters;
using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.Text;

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
}

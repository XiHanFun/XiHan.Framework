#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:I18nFormatHelper
// Guid:8a7b6c5d-4e3f-2c1b-9a8d-7f6e5d4c3b2a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/29 00:48:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;
using System.Text;

namespace XiHan.Framework.Utils.Localization;

/// <summary>
/// 国际化格式化处理帮助类
/// </summary>
public static class I18NFormatHelper
{
    /// <summary>
    /// 格式化日期时间
    /// </summary>
    /// <param name="dateTime">日期时间</param>
    /// <param name="format">格式字符串</param>
    /// <param name="culture">文化</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatDateTime(DateTime dateTime, string format, CultureInfo culture)
    {
        return dateTime.ToString(format, culture);
    }

    /// <summary>
    /// 格式化日期时间
    /// </summary>
    /// <param name="dateTime">日期时间</param>
    /// <param name="format">格式字符串</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatDateTime(DateTime dateTime, string format)
    {
        return FormatDateTime(dateTime, format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 格式化数字
    /// </summary>
    /// <param name="number">数字</param>
    /// <param name="format">格式字符串</param>
    /// <param name="culture">文化</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatNumber(double number, string format, CultureInfo culture)
    {
        return number.ToString(format, culture);
    }

    /// <summary>
    /// 格式化数字
    /// </summary>
    /// <param name="number">数字</param>
    /// <param name="format">格式字符串</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatNumber(double number, string format)
    {
        return FormatNumber(number, format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 格式化百分比
    /// </summary>
    /// <param name="number">数字</param>
    /// <param name="format">格式字符串</param>
    /// <param name="culture">文化</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatPercent(double number, string format, CultureInfo culture)
    {
        return number.ToString(format + "%", culture);
    }

    /// <summary>
    /// 格式化百分比
    /// </summary>
    /// <param name="number">数字</param>
    /// <param name="format">格式字符串</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatPercent(double number, string format)
    {
        return FormatPercent(number, format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <param name="culture">文化</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatFileSize(long bytes, CultureInfo culture)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return string.Format(culture, "{0:0.##} {1}", len, sizes[order]);
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatFileSize(long bytes)
    {
        return FormatFileSize(bytes, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 格式化电话号码
    /// </summary>
    /// <param name="phoneNumber">电话号码</param>
    /// <param name="format">格式字符串</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatPhoneNumber(string phoneNumber, string format)
    {
        if (string.IsNullOrEmpty(phoneNumber))
        {
            return string.Empty;
        }

        var digits = new string([.. phoneNumber.Where(char.IsDigit)]);
        var result = new StringBuilder();
        var digitIndex = 0;

        foreach (var c in format)
        {
            if (c == '#')
            {
                if (digitIndex < digits.Length)
                {
                    result.Append(digits[digitIndex++]);
                }
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 格式化字符串
    /// </summary>
    /// <param name="format">格式字符串</param>
    /// <param name="args">参数</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatString(string format, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, format, args);
    }

    /// <summary>
    /// 格式化字符串
    /// </summary>
    /// <param name="culture">文化</param>
    /// <param name="format">格式字符串</param>
    /// <param name="args">参数</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatString(CultureInfo culture, string format, params object[] args)
    {
        return string.Format(culture, format, args);
    }
}

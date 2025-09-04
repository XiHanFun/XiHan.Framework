#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DataChecker
// Guid:caf51c02-fe14-471c-b1d5-b938d5e42e3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/13 13:44:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 数据验证器
/// </summary>
public static class ValidateHelper
{
    /// <summary>
    /// Guid 格式验证(a480500f-a181-4d3d-8ada-461f69eecfdd)
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsGuid(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return Guid.TryParse(checkValue, out _);
    }

    /// <summary>
    /// Email 地址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsEmail(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        try
        {
            var mailAddress = new MailAddress(checkValue);
            return mailAddress.Address == checkValue;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 数字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumber(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return checkValue.All(char.IsDigit);
    }

    /// <summary>
    /// 是不是 Int 型
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static bool IsInt(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return int.TryParse(source, out _);
    }

    /// <summary>
    /// 整数或者小数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberIntOrDouble(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return double.TryParse(checkValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// 非零的正整数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberPositiveIntNotZero(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return int.TryParse(checkValue, out var result) && result > 0;
    }

    /// <summary>
    /// 非零的负整数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberNegativeIntNotZero(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return int.TryParse(checkValue, out var result) && result < 0;
    }

    /// <summary>
    /// 字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsLetter(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return checkValue.All(char.IsLetter);
    }

    /// <summary>
    /// 大写字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsLetterCapital(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return checkValue.All(char.IsUpper);
    }

    /// <summary>
    /// 小写字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsLetterLower(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return checkValue.All(char.IsLower);
    }

    /// <summary>
    /// 数字或英文字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberOrLetter(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return checkValue.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// 验证字符串长度是否在限定范围内
    /// </summary>
    /// <param name="checkValue">要验证的字符串</param>
    /// <param name="minLength">最小长度</param>
    /// <param name="maxLength">最大长度</param>
    /// <returns>是否符合要求</returns>
    public static bool IsLengthInRange(string? checkValue, int minLength, int maxLength)
    {
        if (checkValue is null)
        {
            return false;
        }

        if (minLength < 0 || maxLength < minLength)
        {
            return false;
        }

        var length = checkValue.Length;
        return length >= minLength && length <= maxLength;
    }

    /// <summary>
    /// 是否网址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsUrl(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return Uri.TryCreate(checkValue, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// 是否有效的URI（包括文件、FTP等协议）
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsUri(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return Uri.TryCreate(checkValue, UriKind.Absolute, out _);
    }

    /// <summary>
    /// 验证日期
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsDateTime(string checkValue)
    {
        try
        {
            return DateTime.TryParse(checkValue, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 是否 IP 地址（支持 IPv4 和 IPv6）
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsIp(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return IPAddress.TryParse(checkValue, out _);
    }

    /// <summary>
    /// 是否 IPv4 地址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsIpv4(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return IPAddress.TryParse(checkValue, out var address) &&
               address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    /// <summary>
    /// 是否 IPv6 地址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsIpv6(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return IPAddress.TryParse(checkValue, out var address) &&
               address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
    }

    /// <summary>
    /// 验证是否为有效的Base64字符串
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsBase64(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        try
        {
            Convert.FromBase64String(checkValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证是否为MAC地址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsMacAddress(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        var parts = checkValue.Split(':', '-');
        if (parts.Length != 6)
        {
            return false;
        }

        return parts.All(part => part.Length == 2 &&
                                int.TryParse(part, NumberStyles.HexNumber, null, out _));
    }

    /// <summary>
    /// 验证是否为端口号（1-65535）
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsPort(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        return int.TryParse(checkValue, out var port) &&
               port >= 1 && port <= 65535;
    }

    /// <summary>
    /// 验证是否为十六进制颜色代码
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsHexColor(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        if (!checkValue.StartsWith('#'))
        {
            return false;
        }

        var hex = checkValue[1..];
        if (hex.Length is not 3 and not 6)
        {
            return false;
        }

        return hex.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
    }

    /// <summary>
    /// 验证是否为有效的文件名
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsValidFileName(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        return !checkValue.Any(c => invalidChars.Contains(c));
    }

    /// <summary>
    /// 验证是否为有效的文件路径
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsValidFilePath(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        try
        {
            Path.GetFullPath(checkValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证是否为JSON格式字符串
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsJson(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        try
        {
            JsonDocument.Parse(checkValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证是否为信用卡号（使用Luhn算法）
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsCreditCard(string checkValue)
    {
        if (string.IsNullOrWhiteSpace(checkValue))
        {
            return false;
        }

        // 移除所有非数字字符
        var digits = checkValue.Where(char.IsDigit).ToArray();

        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        // Luhn算法验证
        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = int.Parse(digits[i].ToString());

            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n = (n % 10) + 1;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// 验证身份证是否有效
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberPeople(string checkValue)
    {
        switch (checkValue.Length)
        {
            case 18:
                {
                    var check = IsNumberPeople18(checkValue);
                    return check;
                }
            case 15:
                {
                    var check = IsNumberPeople15(checkValue);
                    return check;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// 身份证号(18位数字)
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberPeople18(string checkValue)
    {
        // 数字验证
        if (long.TryParse(checkValue[..17], out var n) == false || n < Math.Pow(10, 16) ||
            long.TryParse(checkValue.Replace('x', '0').Replace('X', '0'), out _) == false)
        {
            return false;
        }

        // 省份验证
        const string Address = "11x22x35x44x53x12x23x36x45x54x13x31x37x46x61x14x32x41x50x62x15x33x42x51x63x21x34x43x52x64x65x71x81x82x91";
        if (!Address.Contains(checkValue[..2], StringComparison.CurrentCulture))
        {
            return false;
        }

        // 生日验证
        var birth = checkValue.Substring(6, 8).Insert(6, "-").Insert(4, "-");
        if (!DateTime.TryParse(birth, out _))
        {
            return false;
        }

        // 校验码验证
        var arrVerifyCode = "1,0,x,9,8,7,6,5,4,3,2".Split(',');
        var wi = "7,9,10,5,8,4,2,1,6,3,7,9,10,5,8,4,2".Split(',');
        var ai = checkValue[..17].ToCharArray();
        var sum = 0;
        for (var i = 0; i < 17; i++)
        {
            sum += int.Parse(wi[i]) * int.Parse(ai[i].ToString());
        }

        _ = Math.DivRem(sum, 11, out var y);
        return arrVerifyCode[y].Equals(checkValue.Substring(17, 1), StringComparison.InvariantCultureIgnoreCase);
        // 符合 GB11643-1999标准
    }

    /// <summary>
    /// 身份证号(15位数字)
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberPeople15(string checkValue)
    {
        // 数字验证
        if (long.TryParse(checkValue, out var n) == false || n < Math.Pow(10, 14))
        {
            return false;
        }

        // 省份验证
        const string Address = "11x22x35x44x53x12x23x36x45x54x13x31x37x46x61x14x32x41x50x62x15x33x42x51x63x21x34x43x52x64x65x71x81x82x91";
        if (!Address.Contains(checkValue[..2], StringComparison.CurrentCulture))
        {
            return false;
        }

        // 生日验证
        var birth = checkValue.Substring(6, 6).Insert(4, "-").Insert(2, "-");
        return DateTime.TryParse(birth, out _);
    }
}

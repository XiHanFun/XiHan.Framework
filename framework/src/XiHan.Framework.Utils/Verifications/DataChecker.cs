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

using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Verifications;

/// <summary>
/// DataChecker
/// </summary>
public static class DataChecker
{
    #region 验证输入字符串是否与模式字符串匹配

    /// <summary>
    /// 验证输入字符串是否与模式字符串匹配，匹配返回 true
    /// </summary>
    /// <param name="input">输入的字符串</param>
    /// <param name="pattern">模式字符串</param>
    /// <param name="options">筛选条件</param>
    public static bool IsMatch(string input, string pattern, RegexOptions options = RegexOptions.IgnoreCase)
    {
        return Regex.IsMatch(input, pattern, options);
    }

    #endregion 验证输入字符串是否与模式字符串匹配

    #region 是否 Guid

    /// <summary>
    /// Guid 格式验证(a480500f-a181-4d3d-8ada-461f69eecfdd)
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsGuid(string checkValue)
    {
        return RegexHelper.GuidRegex().IsMatch(checkValue);
    }

    #endregion 是否 Guid

    #region 是否中国电话

    /// <summary>
    /// 电话号码(正确格式为:xxxxxxxxxx 或 xxxxxxxxxxxx)
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberTel(string checkValue)
    {
        return RegexHelper.NumberTelRegex().IsMatch(checkValue);
    }

    #endregion 是否中国电话

    #region 是否身份证

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

    #endregion 是否身份证

    #region 是否邮箱

    /// <summary>
    /// Email 地址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsEmail(string checkValue)
    {
        return RegexHelper.EmailRegex().IsMatch(checkValue);
    }

    #endregion 是否邮箱

    #region 是否数字

    /// <summary>
    /// 数字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumber(string checkValue)
    {
        return RegexHelper.NumberRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 是不是 Int 型
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static bool IsInt(string source)
    {
        return RegexHelper.IntRegex().Match(source).Success && long.Parse(source) is <= 0x7fffffffL and >= -2147483648L;
    }

    /// <summary>
    /// 整数或者小数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberIntOrDouble(string checkValue)
    {
        return RegexHelper.NumberIntOrDoubleRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// N 位的数字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberSeveralN(string checkValue)
    {
        return RegexHelper.NumberSeveralNRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 至少 N 位的数字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberSeveralAtLeastN(string checkValue)
    {
        return RegexHelper.NumberSeveralAtLeastNRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// M 至 N 位的数字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberSeveralMn(string checkValue)
    {
        return RegexHelper.NumberSeveralMnRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 零和非零开头的数字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberBeginZeroOrNotZero(string checkValue)
    {
        return RegexHelper.NumberBeginZeroOrNotZeroRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 2位小数的正实数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberPositiveRealTwoDouble(string checkValue)
    {
        return RegexHelper.NumberPositiveRealTwoDoubleRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 有1-3位小数的正实数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberPositiveRealOneOrThreeDouble(string checkValue)
    {
        return RegexHelper.NumberPositiveRealOneOrThreeDoubleRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 非零的正整数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberPositiveIntNotZero(string checkValue)
    {
        return RegexHelper.NumberPositiveIntNotZeroRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 非零的负整数
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberNegativeIntNotZero(string checkValue)
    {
        return RegexHelper.NumberNegativeIntNotZeroRegex().IsMatch(checkValue);
    }

    #endregion 是否数字

    #region 是否字母

    /// <summary>
    /// 字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsLetter(string checkValue)
    {
        return RegexHelper.LetterRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 大写字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsLetterCapital(string checkValue)
    {
        return RegexHelper.LetterCapitalRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 小写字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsLetterLower(string checkValue)
    {
        return RegexHelper.LetterLowerRegex().IsMatch(checkValue);
    }

    #endregion 是否字母

    #region 是否数字或英文字母

    /// <summary>
    /// 数字或英文字母
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsNumberOrLetter(string checkValue)
    {
        return RegexHelper.NumberOrLetterRegex().IsMatch(checkValue);
    }

    #endregion 是否数字或英文字母

    #region 字符串长度限定

    /// <summary>
    /// 看字符串的长度是不是在限定数之间 一个中文为两个字符
    /// </summary>
    /// <param name="source">字符串</param>
    /// <param name="begin">大于等于</param>
    /// <param name="end">小于等于</param>
    /// <returns></returns>
    public static bool IsLengthStr(string source, int begin, int end)
    {
        var length = RegexHelper.LengthStrRegex().Replace(source, "OK").Length;
        return length > begin || length < end;
    }

    /// <summary>
    /// 长度为3的字符
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsCharThree(string checkValue)
    {
        return RegexHelper.CharThreeRegex().IsMatch(checkValue);
    }

    #endregion 字符串长度限定

    #region 是否邮政编码

    /// <summary>
    /// 邮政编码 6个数字
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static bool IsPostCode(string source)
    {
        return RegexHelper.PostCodeRegex().IsMatch(source);
    }

    #endregion 是否邮政编码

    #region 是否特殊字符

    /// <summary>
    /// 是否含有=，。:等特殊字符
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsCharSpecial(string checkValue)
    {
        return RegexHelper.CharSpecialRegex().IsMatch(checkValue);
    }

    #endregion 是否特殊字符

    #region 是否汉字

    /// <summary>
    /// 包含汉字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsContainChinese(string checkValue)
    {
        return RegexHelper.ContainChineseRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 全部汉字
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsChinese(string checkValue)
    {
        return RegexHelper.ChineseRegex().Matches(checkValue).Count == checkValue.Length;
    }

    #endregion 是否汉字

    #region 是否网址

    /// <summary>
    /// 是否网址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsUrl(string checkValue)
    {
        return RegexHelper.UrlRegex().IsMatch(checkValue);
    }

    #endregion 是否网址

    #region 是否日期

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
    /// 一年的12个月(正确格式为:"01"～"09"和"1"～"12")
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsMonth(string checkValue)
    {
        return RegexHelper.MonthRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 一月的31天(正确格式为:"01"～"09"和"1"～"31")
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsDay(string checkValue)
    {
        return RegexHelper.DayRegex().IsMatch(checkValue);
    }

    #endregion 是否日期

    #region 是否 Ip 地址

    /// <summary>
    /// 是否 Ip 地址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsIpRegex(string checkValue)
    {
        return RegexHelper.IpRegex().IsMatch(checkValue);
    }

    /// <summary>
    /// 是否 Ip 地址
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsIp(string checkValue)
    {
        var result = false;
        try
        {
            var checkValueArg = checkValue.Split('.');
            if (string.Empty != checkValue && checkValue.Length < 16 && checkValueArg.Length == 4)
            {
                for (var i = 0; i < 4; i++)
                {
                    int intCheckValue = Convert.ToInt16(checkValueArg[i]);
                    if (intCheckValue <= 255)
                    {
                        continue;
                    }

                    result = false;
                    return result;
                }

                result = true;
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    #endregion 是否 Ip 地址

    #region 是否 Cron 表达式

    /// <summary>
    /// 是否 Cron 表达式
    /// </summary>
    /// <param name="checkValue"></param>
    /// <returns></returns>
    public static bool IsCron(string checkValue)
    {
        return RegexHelper.CronRegex().IsMatch(checkValue);
    }

    #endregion 是否 Cron 表达式
}

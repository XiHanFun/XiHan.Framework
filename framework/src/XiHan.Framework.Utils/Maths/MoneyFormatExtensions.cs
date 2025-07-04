﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MoneyFormatExtensions
// Guid:76816b67-54c7-4ff0-a582-be0e8faea196
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 5:29:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// 金额扩展方法
/// </summary>
public static class MoneyFormatExtensions
{
    /// <summary>
    /// 格式化金额(由千位转万位，如 12,345,678.90=>1234,5678.90 )
    /// </summary>
    /// <param name="num">需要格式化的金额数值</param>
    /// <returns>格式化后的金额字符串，以万位为单位进行逗号分隔</returns>
    public static string FormatMoneyToString(this decimal num)
    {
        var numStr = num.ToString(CultureInfo.InvariantCulture).ToLowerInvariant();
        string numRes;
        var numDecimal = string.Empty;
        if (numStr.Contains('.'))
        {
            var numInt = numStr.Split('.')[0];
            numDecimal = "." + numStr.Split('.')[1];
            numRes = FormatMoneyStringComma(numInt);
        }
        else
        {
            numRes = FormatMoneyStringComma(numStr);
        }

        return numRes + numDecimal;
    }

    /// <summary>
    /// 金额字符串加逗号格式化
    /// </summary>
    /// <param name="numInt">需要格式化的金额整数部分字符串</param>
    /// <returns>添加了逗号分隔符的金额字符串</returns>
    private static string FormatMoneyStringComma(string numInt)
    {
        if (numInt.Length <= 4)
        {
            return numInt;
        }

        var numNoFormat = numInt[..^4];
        var numFormat = numInt.Substring(numInt.Length - 4, 4);
        return numNoFormat.Length > 4
            ? FormatMoneyStringComma(numNoFormat) + "," + numFormat
            : numNoFormat + "," + numFormat;
    }
}

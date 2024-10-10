#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StringExtensions
// Guid:3630d8a8-77e0-45eb-a1e6-f9a6b5dc26ba
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-12-03 上午 12:30:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 字符串拓展类
/// </summary>
public static class StringExtensions
{
    #region 分割

    /// <summary>
    /// 分割字符串按分割器转换为列表
    /// </summary>
    /// <param name="sourceStr">源字符串</param>
    /// <param name="sepeater">分割器</param>
    /// <param name="isAllowsDuplicates">是否允许重复</param>
    /// <returns></returns>
    public static List<string> GetStrList(this string sourceStr, char sepeater = ',', bool isAllowsDuplicates = true)
    {
        return sourceStr.GetStrEnumerable(sepeater, isAllowsDuplicates).ToList();
    }

    /// <summary>
    /// 分割字符串按分割器转换为数组
    /// </summary>
    /// <param name="sourceStr">源字符串</param>
    /// <param name="sepeater">分割器</param>
    /// <param name="isAllowsDuplicates">是否允许重复</param>
    /// <returns></returns>
    public static string[] GetStrArray(this string sourceStr, char sepeater = ',', bool isAllowsDuplicates = true)
    {
        return sourceStr.GetStrEnumerable(sepeater, isAllowsDuplicates).ToArray();
    }

    /// <summary>
    /// 分割字符串按分割器转换为序列
    /// </summary>
    /// <param name="sourceStr">源字符串</param>
    /// <param name="sepeater">分割器</param>
    /// <param name="isAllowsDuplicates">是否允许重复</param>
    /// <returns></returns>
    public static IEnumerable<string> GetStrEnumerable(this string sourceStr, char sepeater = ',', bool isAllowsDuplicates = true)
    {
        IEnumerable<string> result = sourceStr.Split(sepeater);
        return isAllowsDuplicates ? result : result.Distinct();
    }

    #endregion

    #region 组装

    /// <summary>
    /// 组装列表按分割器转换为字符串
    /// </summary>
    /// <param name="sourceList">源列表</param>
    /// <param name="sepeater">分割器</param>
    /// <param name="isAllowsDuplicates">是否允许重复</param>
    /// <returns></returns>
    public static string GetListStr(this IEnumerable<string> sourceList, char sepeater = ',', bool isAllowsDuplicates = true)
    {
        var sourceEnumerable = sourceList;
        return sourceEnumerable.GetEnumerableStr(sepeater, isAllowsDuplicates);
    }

    /// <summary>
    /// 组装数组按分割器转换为字符串
    /// </summary>
    /// <param name="sourceArray">源数组</param>
    /// <param name="sepeator">分割器</param>
    /// <param name="isAllowsDuplicates">是否允许重复</param>
    /// <returns></returns>
    public static string GetArrayStr(this IEnumerable<string> sourceArray, char sepeator = ',', bool isAllowsDuplicates = true)
    {
        var sourceEnumerable = sourceArray;
        return sourceEnumerable.GetEnumerableStr(sepeator, isAllowsDuplicates);
    }

    /// <summary>
    /// 组装字典按分割器转换为字符串
    /// </summary>
    /// <param name="sourceDictionary">源字典</param>
    /// <param name="sepeater">分割器</param>
    /// <param name="isAllowsDuplicates">是否允许重复</param>
    /// <returns></returns>
    public static string GetDictionaryValueStr(this Dictionary<string, string> sourceDictionary, char sepeater = ',', bool isAllowsDuplicates = true)
    {
        IEnumerable<string> sourceEnumerable = sourceDictionary.Values;
        return sourceEnumerable.GetEnumerableStr(sepeater, isAllowsDuplicates);
    }

    /// <summary>
    /// 组装序列按分割器转换为字符串
    /// </summary>
    /// <param name="sourceEnumerable">源序列</param>
    /// <param name="sepeater">分割器</param>
    /// <param name="isAllowsDuplicates">是否允许重复</param>
    /// <returns></returns>
    public static string GetEnumerableStr(this IEnumerable<string> sourceEnumerable, char sepeater = ',', bool isAllowsDuplicates = true)
    {
        StringBuilder sb = new();
        if (!isAllowsDuplicates) sourceEnumerable = sourceEnumerable.Distinct();

        var enumerable = sourceEnumerable.ToList();
        foreach (var item in enumerable)
            if (item == enumerable.LastOrDefault())
            {
                sb.Append(item);
            }
            else
            {
                sb.Append(item);
                sb.Append(sepeater);
            }

        return sb.ToString();
    }

    #endregion

    #region 删除结尾分割器

    /// <summary>
    /// 删除最后结尾的指定字符后的字符
    /// </summary>
    /// <param name="sourceStr">源字符串</param>
    /// <param name="sepeater">分割器</param>
    /// <returns></returns>
    public static string DelLastChar(this string sourceStr, char sepeater = ',')
    {
        return sourceStr[..sourceStr.LastIndexOf(sepeater)];
    }

    #endregion

    #region 半角全角转换

    /// <summary>
    /// 半角转全角的函数(SBC case)
    /// </summary>
    /// <param name="sourceStr">源字符串</param>
    /// <returns></returns>
    public static string ToSbc(this string sourceStr)
    {
        var c = sourceStr.ToCharArray();
        for (var i = 0; i < c.Length; i++)
        {
            if (c[i] == 32)
            {
                c[i] = (char)12288;
                continue;
            }

            if (c[i] < 127) c[i] = (char)(c[i] + 65248);
        }

        return new string(c);
    }

    /// <summary>
    /// 全角转半角的函数(SBC case)
    /// </summary>
    /// <param name="sourceStr">源字符串</param>
    /// <returns></returns>
    public static string ToDbc(this string sourceStr)
    {
        var c = sourceStr.ToCharArray();
        for (var i = 0; i < c.Length; i++)
        {
            if (c[i] == 12288)
            {
                c[i] = (char)32;
                continue;
            }

            if (c[i] is > (char)65280 and < (char)65375) c[i] = (char)(c[i] - 65248);
        }

        return new string(c);
    }

    #endregion

    #region 转换为纯字符串

    /// <summary>
    /// 将字符串样式转换为纯字符串
    /// </summary>
    /// <param name="sourceStr"></param>
    /// <param name="splitString"></param>
    /// <returns></returns>
    public static string GetCleanStyle(this string? sourceStr, string splitString)
    {
        string? result;
        // 如果为空，返回空值
        if (sourceStr == null)
        {
            result = string.Empty;
        }
        else
        {
            // 返回去掉分隔符
            var newString = sourceStr.Replace(splitString, string.Empty);
            result = newString;
        }

        return result;
    }

    #endregion

    #region 转换为新样式

    /// <summary>
    /// 将字符串转换为新样式
    /// </summary>
    /// <param name="sourceStr"></param>
    /// <param name="newStyle"></param>
    /// <param name="splitString"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static string GetNewStyle(this string? sourceStr, string? newStyle, string splitString, out string error)
    {
        string? returnValue;
        // 如果输入空值，返回空，并给出错误提示
        if (sourceStr == null)
        {
            returnValue = string.Empty;
            error = "请输入需要划分格式的字符串";
        }
        else
        {
            //检查传入的字符串长度和样式是否匹配,如果不匹配，则说明使用错误，给出错误信息并返回空值
            var sourceStrLength = sourceStr.Length;
            var newStyleLength = GetCleanStyle(newStyle, splitString).Length;
            if (sourceStrLength != newStyleLength)
            {
                returnValue = string.Empty;
                error = "样式格式的长度与输入的字符长度不符，请重新输入";
            }
            else
            {
                // 检查新样式中分隔符的位置
                StringBuilder newStr = new();
                if (newStyle != null)
                    for (var i = 0; i < newStyle.Length; i++)
                        if (newStyle.Substring(i, 1) == splitString)
                            newStr.Append(i + ",");

                if (!string.IsNullOrWhiteSpace(newStr.ToString()))
                {
                    // 将分隔符放在新样式中的位置
                    var str = newStr.ToString().Split(',');
                    sourceStr = str.Aggregate(sourceStr, (current, bb) => current.Insert(int.Parse(bb), splitString));
                }

                // 给出最后的结果
                returnValue = sourceStr;
                // 因为是正常的输出，没有错误
                error = string.Empty;
            }
        }

        return returnValue;
    }

    #endregion

    #region 是否SQL安全字符串

    /// <summary>
    /// 是否SQL安全字符串
    /// </summary>
    /// <param name="sourceStr"></param>
    /// <param name="isDel"></param>
    /// <returns></returns>
    public static string SqlSafeString(this string sourceStr, bool isDel)
    {
        if (isDel)
        {
            sourceStr = sourceStr.Replace(@"'", string.Empty);
            sourceStr = sourceStr.Replace(@"""", string.Empty);
            return sourceStr;
        }

        sourceStr = sourceStr.Replace(@"'", "&#39;");
        sourceStr = sourceStr.Replace(@"""", "&#34;");
        return sourceStr;
    }

    #endregion

    #region 检查验证

    /// <summary>
    /// 检查一个字符串是否是纯数字构成的，一般用于查询字符串参数的有效性验证(0除外)
    /// </summary>
    /// <param name="value">需验证的字符串</param>
    /// <returns>是否合法的bool值。</returns>
    public static bool IsNumberId(this string? value)
    {
        return IsValidateStr("^[1-9]*[0-9]*$", value);
    }

    /// <summary>
    /// 验证一个字符串是否符合指定的正则表达式
    /// </summary>
    /// <param name="express"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsValidateStr(this string express, string? value)
    {
        if (value == null) return false;

        Regex myRegex = new(express);
        return value.Length != 0 && myRegex.IsMatch(value);
    }

    #endregion

    #region 得到字符串长度

    /// <summary>
    /// 得到字符串长度(一个汉字长度为2)
    /// </summary>
    /// <param name="inputString">参数字符串</param>
    /// <returns></returns>
    public static int StrLength(this string inputString)
    {
        ASCIIEncoding ascii = new();
        var tempLen = 0;
        var s = ascii.GetBytes(inputString);
        foreach (var t in s)
            if (t == 63)
                tempLen += 2;
            else
                tempLen += 1;

        return tempLen;
    }

    #endregion

    #region 截取指定长度字符串

    /// <summary>
    /// 截取指定长度字符串
    /// </summary>
    /// <param name="inputString">要处理的字符串</param>
    /// <param name="len">指定长度</param>
    /// <returns>返回处理后的字符串</returns>
    public static string ClipString(this string inputString, int len)
    {
        var isShowFix = false;
        if (len > 0 && len % 2 == 1)
        {
            isShowFix = true;
            len--;
        }

        ASCIIEncoding ascii = new();
        var tempLen = 0;
        StringBuilder sb = new();
        var s = ascii.GetBytes(inputString);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == 63)
                tempLen += 2;
            else
                tempLen += 1;

            try
            {
                sb.Append(inputString.AsSpan(i, 1));
            }
            catch
            {
                break;
            }

            if (tempLen > len) break;
        }

        var myByte = Encoding.Default.GetBytes(inputString);
        if (isShowFix && myByte.Length > len) sb.Append('…');

        return sb.ToString();
    }

    #endregion

    #region HTML转行成TEXT

    /// <summary>
    /// HTML转行成TEXT
    /// </summary>
    /// <param name="strHtml"></param>
    /// <returns></returns>
    public static string HtmlToTxt(this string strHtml)
    {
        string[] aryReg =
        [
            @"<script[^>]*?>.*?</script>",
            @"<(\/\s*)?!?((\w+:)?\w+)(\w+(\s*=?\s*(([""'])(\\[""'tbnr]|[^\7])*?\7|\w+)|.{0})|\s)*?(\/\s*)?>",
            @"([\r\n])[\s]+",
            @"&(quot|#34);",
            @"&(amp|#38);",
            @"&(lt|#60);",
            @"&(gt|#62);",
            @"&(nbsp|#160);",
            @"&(iexcl|#161);",
            @"&(cent|#162);",
            @"&(pound|#163);",
            @"&(copy|#169);",
            @"&#(\d+);",
            @"-->",
            @"<!--.*\n"
        ];

        var strOutput = aryReg.Select(t => new Regex(t, RegexOptions.IgnoreCase)).Aggregate(strHtml, (current, regex) => regex.Replace(current, string.Empty));
        strOutput = strOutput.Replace("<", string.Empty).Replace(">", string.Empty).Replace("\r\n", string.Empty);
        return strOutput;
    }

    #endregion

    #region 首字母处理

    /// <summary>
    /// 首字母大写
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string FirstToUpper(this string value)
    {
        return value[..1].ToUpperInvariant() + value[1..];
    }

    /// <summary>
    /// 首字母小写
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string FirstToLower(this string value)
    {
        return value[..1].ToLowerInvariant() + value[1..];
    }

    #endregion

    #region 整体替换

    /// <summary>
    /// 字符串整体替换
    /// </summary>
    /// <param name="content"></param>
    /// <param name="oldStr"></param>
    /// <param name="newStr"></param>
    /// <returns></returns>
    public static string FormatReplaceStr(this string content, string oldStr, string newStr)
    {
        // 没有替换字符串直接返回源字符串
        if (!content.Contains(oldStr, StringComparison.CurrentCulture)) return content;
        // 有替换字符串开始替换
        StringBuilder strBuffer = new();
        var start = 0;
        var end = 0;
        // 查找替换内容，把它之前和上一个替换内容之后的字符串拼接起来
        while (true)
        {
            start = content.IndexOf(oldStr, start, StringComparison.Ordinal);
            if (start == -1) break;

            strBuffer.Append(content[end..start]);
            strBuffer.Append(newStr);
            start += oldStr.Length;
            end = start;
        }

        // 查找到最后一个位置之后，把剩下的字符串拼接进去
        strBuffer.Append(content[end..]);
        return strBuffer.ToString();
    }

    #endregion
}
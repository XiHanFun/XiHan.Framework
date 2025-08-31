#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RegexHelper
// Guid:351b39db-a1a2-4d26-94bb-96a924fba528
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-05-09 上午 01:16:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 字符验证帮助类
/// </summary>
public static partial class RegexHelper
{
    /// <summary>
    /// 验证输入字符串是否与模式字符串匹配，匹配返回 true
    /// </summary>
    [GeneratedRegex(@"^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$", RegexOptions.IgnoreCase)]
    public static partial Regex GuidRegex();

    /// <summary>
    /// 验证电话号码是否符合格式
    /// </summary>
    [GeneratedRegex(@"^(\d{3,4})\d{7,8}$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberTelRegex();

    /// <summary>
    /// 验证邮箱地址是否符合格式
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9](([\.\-]?[a-zA-Z0-9]+)*)@([A-Za-z0-9]+)(([\.\-]?[a-zA-Z0-9]+)*)\.([A-Za-z]{2,})$", RegexOptions.IgnoreCase)]
    public static partial Regex EmailRegex();

    /// <summary>
    /// 验证一个或多个数字
    /// </summary>
    [GeneratedRegex(@"(\d+)", RegexOptions.IgnoreCase)]
    public static partial Regex OneOrMoreNumbersRegex();

    /// <summary>
    /// 验证是否为整数
    /// </summary>
    [GeneratedRegex(@"^(-){0,1}\d+$", RegexOptions.IgnoreCase)]
    public static partial Regex IntRegex();

    /// <summary>
    /// 验证是否为数字
    /// </summary>
    [GeneratedRegex(@"^[0-9]*$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberRegex();

    /// <summary>
    /// 验证是否为整数或小数
    /// </summary>
    [GeneratedRegex(@"^[0-9]+\.{0,1}[0-9]{0,2}$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberIntOrDoubleRegex();

    /// <summary>
    /// 验证是否为N位数字
    /// </summary>
    [GeneratedRegex(@"^\d{n}$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberSeveralNRegex();

    /// <summary>
    /// 验证是否为至少N位数字
    /// </summary>
    [GeneratedRegex(@"^\d{n,}$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberSeveralAtLeastNRegex();

    /// <summary>
    /// 验证是否为M至N位数字
    /// </summary>
    [GeneratedRegex(@"^\d{m,n}$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberSeveralMnRegex();

    /// <summary>
    /// 验证是否为零或非零开头的数字
    /// </summary>
    [GeneratedRegex(@"^(0|[1-9] [0-9]*)$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberBeginZeroOrNotZeroRegex();

    /// <summary>
    /// 验证是否为2位小数的正实数
    /// </summary>
    [GeneratedRegex(@"^[0-9]+(.[0-9]{2})?$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberPositiveRealTwoDoubleRegex();

    /// <summary>
    /// 验证是否为1-3位小数的正实数
    /// </summary>
    [GeneratedRegex(@"^[0-9]+(.[0-9]{1,3})?$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberPositiveRealOneOrThreeDoubleRegex();

    /// <summary>
    /// 验证是否为非零的正整数
    /// </summary>
    [GeneratedRegex(@"^\+?[1-9][0-9]*$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberPositiveIntNotZeroRegex();

    /// <summary>
    /// 验证是否为非零的负整数
    /// </summary>
    [GeneratedRegex(@"^\-?[1-9][0-9]*$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberNegativeIntNotZeroRegex();

    /// <summary>
    /// 验证是否为字母
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z]+$", RegexOptions.IgnoreCase)]
    public static partial Regex LetterRegex();

    /// <summary>
    /// 验证是否为大写字母
    /// </summary>
    [GeneratedRegex(@"^[A-Z]+$", RegexOptions.IgnoreCase)]
    public static partial Regex LetterCapitalRegex();

    /// <summary>
    /// 验证是否为小写字母
    /// </summary>
    [GeneratedRegex(@"^[a-z]+$", RegexOptions.IgnoreCase)]
    public static partial Regex LetterLowerRegex();

    /// <summary>
    /// 验证是否为数字或英文字母
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9]+$", RegexOptions.IgnoreCase)]
    public static partial Regex NumberOrLetterRegex();

    /// <summary>
    /// 验证字符串长度是否在限定范围内
    /// </summary>
    [GeneratedRegex(@"[^\x00-\xff]", RegexOptions.IgnoreCase)]
    public static partial Regex LengthStrRegex();

    /// <summary>
    /// 验证是否为长度为3的字符
    /// </summary>
    [GeneratedRegex(@"^.{3}$", RegexOptions.IgnoreCase)]
    public static partial Regex CharThreeRegex();

    /// <summary>
    /// 验证是否为邮政编码
    /// </summary>
    [GeneratedRegex(@"^\d{6}$", RegexOptions.IgnoreCase)]
    public static partial Regex PostCodeRegex();

    /// <summary>
    /// 验证是否含有特殊字符
    /// </summary>
    [GeneratedRegex(@"[^%&',;=?$\x22]+", RegexOptions.IgnoreCase)]
    public static partial Regex CharSpecialRegex();

    /// <summary>
    /// 验证是否包含汉字
    /// </summary>
    [GeneratedRegex(@"^[\u4e00-\u9fa5]{0,}$", RegexOptions.IgnoreCase)]
    public static partial Regex ContainChineseRegex();

    /// <summary>
    /// 验证是否为汉字
    /// </summary>
    [GeneratedRegex(@"[一-龥]", RegexOptions.IgnoreCase, "zh-CN")]
    public static partial Regex ChineseRegex();

    /// <summary>
    /// 验证是否为网址
    /// </summary>
    [GeneratedRegex(@"^(((file|gopher|news|nntp|telnet|http|ftp|https|ftps|sftp)://)|(www\.))+(([a-zA-Z0-9\.-]+\.[a-zA-Z]{2,6})|([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(/[a-zA-Z0-9\&amp;%\./-~-]*)?$", RegexOptions.IgnoreCase)]
    public static partial Regex UrlRegex();

    /// <summary>
    /// 验证是否为请求安全参数字符串
    /// </summary>
    [GeneratedRegex(@"(?<=password=|passwd=|pwd=|secret=|token=)[^&]+", RegexOptions.IgnoreCase)]
    public static partial Regex RequestSecurityParamsRegex();

    /// <summary>
    /// 验证是否为月份
    /// </summary>
    [GeneratedRegex(@"^^(0?[1-9]|1[0-2])$", RegexOptions.IgnoreCase)]
    public static partial Regex MonthRegex();

    /// <summary>
    /// 验证是否为日期
    /// </summary>
    [GeneratedRegex(@"^((0?[1-9])|((1|2)[0-9])|30|31)$", RegexOptions.IgnoreCase)]
    public static partial Regex DayRegex();

    /// <summary>
    /// 验证是否为IP地址
    /// </summary>
    [GeneratedRegex(@"^(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9])\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[0-9])$", RegexOptions.IgnoreCase)]
    public static partial Regex IpRegex();

    /// <summary>
    /// 验证是否为Cron表达式
    /// </summary>
    [GeneratedRegex(@"^\\s*($|#|\\w+\\s*=|(\\?|\\*|(?:[0-5]?\\d)(?:(?:-|\\/|\\,)(?:[0-5]?\\d))?(?:,(?:[0-5]?\\d)(?:(?:-|\\/|\\,)(?:[0-5]?\\d))?)*)\\s+(\\?|\\*|(?:[0-5]?\\d)(?:(?:-|\\/|\\,)(?:[0-5]?\\d))?(?:,(?:[0-5]?\\d)(?:(?:-|\\/|\\,)(?:[0-5]?\\d))?)*)\\s+(\\?|\\*|(?:[01]?\\d|2[0-3])(?:(?:-|\\/|\\,)(?:[01]?\\d|2[0-3]))?(?:,(?:[01]?\\d|2[0-3])(?:(?:-|\\/|\\,)(?:[01]?\\d|2[0-3]))?)*)\\s+(\\?|\\*|(?:0?[1-9]|[12]\\d|3[01])(?:(?:-|\\/|\\,)(?:0?[1-9]|[12]\\d|3[01]))?(?:,(?:0?[1-9]|[12]\\d|3[01])(?:(?:-|\\/|\\,)(?:0?[1-9]|[12]\\d|3[01]))?)*)\\s+(\\?|\\*|(?:[1-9]|1[012])(?:(?:-|\\/|\\,)(?:[1-9]|1[012]))?(?:L|W)?(?:,(?:[1-9]|1[012])(?:(?:-|\\/|\\,)(?:[1-9]|1[012]))?(?:L|W)?)*|\\?|\\*|(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)(?:(?:-)(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))?(?:,(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)(?:(?:-)(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))?)*)\\s+(\\?|\\*|(?:[0-6])(?:(?:-|\\/|\\,|#)(?:[0-6]))?(?:L)?(?:,(?:[0-6])(?:(?:-|\\/|\\,|#)(?:[0-6]))?(?:L)?)*|\\?|\\*|(?:MON|TUE|WED|THU|FRI|SAT|SUN)(?:(?:-)(?:MON|TUE|WED|THU|FRI|SAT|SUN))?(?:,(?:MON|TUE|WED|THU|FRI|SAT|SUN)(?:(?:-)(?:MON|TUE|WED|THU|FRI|SAT|SUN))?)*)(|\\s)+(\\?|\\*|(?:|\\d{4})(?:(?:-|\\/|\\,)(?:|\\d{4}))?(?:,(?:|\\d{4})(?:(?:-|\\/|\\,)(?:|\\d{4}))?)*))$", RegexOptions.IgnoreCase)]
    public static partial Regex CronRegex();

    /// <summary>
    /// 验证是否为 Windows 普通文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(?:[a-zA-Z]:\\|\\\\)?(?:[^\\\/:*?""<>|\r\n]+\\)*[^\\\/:*?""<>|\r\n]+\\?$", RegexOptions.IgnoreCase)]
    public static partial Regex WindowsPathRegex();

    /// <summary>
    /// 验证是否为 Linux 普通文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(\/|\/?([^/\0]+(\/[^/\0]+)*\/?))$", RegexOptions.IgnoreCase)]
    public static partial Regex LinuxPathRegex();

    /// <summary>
    /// 验证是否为虚拟文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(~\/|\/)([a-zA-Z0-9_\-\.]+(\/[a-zA-Z0-9_\-\.]+)*)\/?$", RegexOptions.IgnoreCase)]
    public static partial Regex VirtualPathRegex();

    /// <summary>
    /// 验证是否为嵌入文件路径
    /// </summary>
    [GeneratedRegex(@"^embedded://(?<assembly>[^/]+)/(?<path>.*)$", RegexOptions.IgnoreCase)]
    public static partial Regex EmbeddedPathRegex();

    /// <summary>
    /// 验证是否为内存文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(?i:(?:memory|mem):\/\/).+$", RegexOptions.IgnoreCase)]
    public static partial Regex MemoryPathRegex();

    /// <summary>
    /// 验证是否为 Html 标签
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@">([^<>]*)<", RegexOptions.IgnoreCase)]
    public static partial Regex HtmlTagContentRegex();

    /// <summary>
    /// 验证是否文本分割为句子
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"[^.!?。！？]+[.!?。！？]?", RegexOptions.IgnoreCase)]
    public static partial Regex SentenceSplitterRegex();

    /// <summary>
    /// 验证是否为 Unicode 字符
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\\u([0-9A-Za-z]{4})")]
    public static partial Regex UnicodeRegex();
}
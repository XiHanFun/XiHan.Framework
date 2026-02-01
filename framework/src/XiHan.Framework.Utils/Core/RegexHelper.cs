#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RegexHelper
// Guid:351b39db-a1a2-4d26-94bb-96a924fba528
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/01 17:29:39
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
    /// <param name="input">输入的字符串</param>
    /// <param name="pattern">模式字符串</param>
    /// <param name="options">筛选条件</param>
    public static bool IsMatch(string input, string pattern, RegexOptions options = RegexOptions.IgnoreCase)
    {
        return Regex.IsMatch(input, pattern, options);
    }

    #region 通用

    /// <summary>
    /// 验证是否为空白字符（空格、制表符、换行符等）
    /// </summary>
    [GeneratedRegex(@"^\s*$", RegexOptions.Compiled)]
    public static partial Regex WhitespaceRegex();

    /// <summary>
    /// 验证是否包含表情符号（基础匹配）
    /// </summary>
    [GeneratedRegex(@"[\u2600-\u26FF\u2700-\u27BF]", RegexOptions.Compiled)]
    public static partial Regex EmojiRegex();

    /// <summary>
    /// 验证是否为UUId（包括带连字符和不带连字符的格式）
    /// </summary>
    [GeneratedRegex(@"^[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}$", RegexOptions.Compiled)]
    public static partial Regex UuidRegex();

    /// <summary>
    /// 验证是否为Guid
    /// </summary>
    [GeneratedRegex(@"^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$", RegexOptions.Compiled)]
    public static partial Regex GuidRegex();

    /// <summary>
    /// 验证是否为电话号码
    /// </summary>
    [GeneratedRegex(@"^(\d{3,4})\d{7,8}$", RegexOptions.Compiled)]
    public static partial Regex NumberTelRegex();

    /// <summary>
    /// 验证是否为邮箱地址
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled)]
    public static partial Regex EmailRegex();

    /// <summary>
    /// 验证是否为一个或多个数字
    /// </summary>
    [GeneratedRegex(@"(\d+)", RegexOptions.Compiled)]
    public static partial Regex OneOrMoreNumbersRegex();

    /// <summary>
    /// 验证是否为整数
    /// </summary>
    [GeneratedRegex(@"^(-){0,1}\d+$", RegexOptions.Compiled)]
    public static partial Regex IntRegex();

    /// <summary>
    /// 验证是否为数字
    /// </summary>
    [GeneratedRegex(@"^[0-9]*$", RegexOptions.Compiled)]
    public static partial Regex NumberRegex();

    /// <summary>
    /// 验证是否为小数
    /// </summary>
    [GeneratedRegex(@"^[0-9]+\.{0,1}[0-9]{0,2}$", RegexOptions.Compiled)]
    public static partial Regex NumberIntOrDoubleRegex();

    /// <summary>
    /// 验证是否为4位数字，常用于PIN码
    /// </summary>
    [GeneratedRegex(@"^\d{4}$", RegexOptions.Compiled)]
    public static partial Regex NumberSeveral4Regex();

    /// <summary>
    /// 验证是否为至少4位数字
    /// </summary>
    [GeneratedRegex(@"^\d{4,}$", RegexOptions.Compiled)]
    public static partial Regex NumberSeveralAtLeast4Regex();

    /// <summary>
    /// 验证是否为6位数字，常用于验证码
    /// </summary>
    [GeneratedRegex(@"^\d{6}$", RegexOptions.Compiled)]
    public static partial Regex NumberSeveral6Regex();

    /// <summary>
    /// 验证是否为至少6位数字
    /// </summary>
    [GeneratedRegex(@"^\d{6,}$", RegexOptions.Compiled)]
    public static partial Regex NumberSeveralAtLeast6Regex();

    /// <summary>
    /// 验证是否为零或非零开头的数字
    /// </summary>
    [GeneratedRegex(@"^(0|[1-9][0-9]*)$", RegexOptions.Compiled)]
    public static partial Regex NumberBeginZeroOrNotZeroRegex();

    /// <summary>
    /// 验证是否为2位小数的正实数
    /// </summary>
    [GeneratedRegex(@"^[0-9]+(.[0-9]{2})?$", RegexOptions.Compiled)]
    public static partial Regex NumberPositiveRealTwoDoubleRegex();

    /// <summary>
    /// 验证是否为1-3位小数的正实数
    /// </summary>
    [GeneratedRegex(@"^[0-9]+(.[0-9]{1,3})?$", RegexOptions.Compiled)]
    public static partial Regex NumberPositiveRealOneOrThreeDoubleRegex();

    /// <summary>
    /// 验证是否为非零的正整数
    /// </summary>
    [GeneratedRegex(@"^\+?[1-9][0-9]*$", RegexOptions.Compiled)]
    public static partial Regex NumberPositiveIntNotZeroRegex();

    /// <summary>
    /// 验证是否为非零的负整数
    /// </summary>
    [GeneratedRegex(@"^\-?[1-9][0-9]*$", RegexOptions.Compiled)]
    public static partial Regex NumberNegativeIntNotZeroRegex();

    /// <summary>
    /// 验证是否为字母
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z]+$", RegexOptions.Compiled)]
    public static partial Regex LetterRegex();

    /// <summary>
    /// 验证是否为大写字母
    /// </summary>
    [GeneratedRegex(@"^[A-Z]+$", RegexOptions.Compiled)]
    public static partial Regex LetterCapitalRegex();

    /// <summary>
    /// 验证是否为小写字母
    /// </summary>
    [GeneratedRegex(@"^[a-z]+$", RegexOptions.Compiled)]
    public static partial Regex LetterLowerRegex();

    /// <summary>
    /// 验证是否为数字或英文字母
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9]+$", RegexOptions.Compiled)]
    public static partial Regex NumberOrLetterRegex();

    /// <summary>
    /// 验证是否为邮政编码
    /// </summary>
    [GeneratedRegex(@"^\d{6}$", RegexOptions.Compiled)]
    public static partial Regex PostCodeRegex();

    /// <summary>
    /// 验证是否含有特殊字符
    /// </summary>
    [GeneratedRegex(@"[^%&',;=?$\x22]+", RegexOptions.Compiled)]
    public static partial Regex CharSpecialRegex();

    /// <summary>
    /// 验证是否包含汉字
    /// </summary>
    [GeneratedRegex(@"^[\u4e00-\u9fa5]{0,}$", RegexOptions.Compiled)]
    public static partial Regex ContainChineseRegex();

    /// <summary>
    /// 验证是否为汉字
    /// </summary>
    [GeneratedRegex(@"[一-龥]", RegexOptions.Compiled, "zh-CN")]
    public static partial Regex ChineseRegex();

    /// <summary>
    /// 验证是否为网址
    /// </summary>
    [GeneratedRegex(@"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$", RegexOptions.Compiled)]
    public static partial Regex UrlRegex();

    /// <summary>
    /// 验证是否为请求安全参数字符串
    /// </summary>
    [GeneratedRegex(@"(?<=password=|passwd=|pwd=|secret=|token=)[^&]+", RegexOptions.Compiled)]
    public static partial Regex RequestSecurityParamsRegex();

    /// <summary>
    /// 验证是否为 Html 标签
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@">([^<>]*)<", RegexOptions.Compiled)]
    public static partial Regex HtmlTagContentRegex();

    /// <summary>
    /// 验证是否文本分割为句子
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"[^.!?。！？]+[.!?。！？]?", RegexOptions.Compiled)]
    public static partial Regex SentenceSplitterRegex();

    /// <summary>
    /// 验证是否为 Unicode 字符
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\\u([0-9A-Za-z]{4})")]
    public static partial Regex UnicodeRegex();

    #endregion

    #region 密码验证

    /// <summary>
    /// 验证是否为强密码（至少8位，包含大小写字母、数字和特殊字符）
    /// </summary>
    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", RegexOptions.Compiled)]
    public static partial Regex StrongPasswordRegex();

    /// <summary>
    /// 验证是否为中等强度密码（至少6位，包含字母和数字）
    /// </summary>
    [GeneratedRegex(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d@$!%*?&]{6,}$", RegexOptions.Compiled)]
    public static partial Regex MediumPasswordRegex();

    /// <summary>
    /// 验证是否为弱密码（仅字母或仅数字）
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z]+$|^\d+$", RegexOptions.Compiled)]
    public static partial Regex WeakPasswordRegex();

    #endregion

    #region 颜色代码验证

    /// <summary>
    /// 验证是否为十六进制颜色代码
    /// </summary>
    [GeneratedRegex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", RegexOptions.Compiled)]
    public static partial Regex HexColorRegex();

    /// <summary>
    /// 验证是否为RGB颜色代码
    /// </summary>
    [GeneratedRegex(@"^rgb\(([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5]),\s*([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5]),\s*([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5])\)$", RegexOptions.Compiled)]
    public static partial Regex RgbColorRegex();

    /// <summary>
    /// 验证是否为RGBA颜色代码
    /// </summary>
    [GeneratedRegex(@"^rgba\(([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5]),\s*([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5]),\s*([01]?[0-9]?[0-9]|2[0-4][0-9]|25[0-5]),\s*(0(\.\d+)?|1(\.0+)?)\)$", RegexOptions.Compiled)]
    public static partial Regex RgbaColorRegex();

    #endregion

    #region 版本号验证

    /// <summary>
    /// 验证是否为语义版本号（Semantic Versioning）
    /// </summary>
    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$", RegexOptions.Compiled)]
    public static partial Regex SemanticVersionRegex();

    /// <summary>
    /// 验证是否为简单版本号（如 1.0.0）
    /// </summary>
    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled)]
    public static partial Regex SimpleVersionRegex();

    #endregion

    #region 文件和路径验证

    /// <summary>
    /// 验证是否为 Windows 普通文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(?:[a-zA-Z]:\\|\\\\)?(?:[^\\\/:*?""<>|\r\n]+\\)*[^\\\/:*?""<>|\r\n]+\\?$", RegexOptions.Compiled)]
    public static partial Regex WindowsPathRegex();

    /// <summary>
    /// 验证是否为 Linux 普通文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(\/|\/?([^/\0]+(\/[^/\0]+)*\/?))$", RegexOptions.Compiled)]
    public static partial Regex LinuxPathRegex();

    /// <summary>
    /// 验证是否为虚拟文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(~\/|\/)([a-zA-Z0-9_\-\.]+(\/[a-zA-Z0-9_\-\.]+)*)\/?$", RegexOptions.Compiled)]
    public static partial Regex VirtualPathRegex();

    /// <summary>
    /// 验证是否为嵌入文件路径
    /// </summary>
    [GeneratedRegex(@"^embedded://(?<assembly>[^/]+)/(?<path>.*)$", RegexOptions.Compiled)]
    public static partial Regex EmbeddedPathRegex();

    /// <summary>
    /// 验证是否为内存文件路径
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"^(?i:(?:memory|mem):\/\/).+$", RegexOptions.Compiled)]
    public static partial Regex MemoryPathRegex();

    /// <summary>
    /// 验证是否为图片文件扩展名
    /// </summary>
    [GeneratedRegex(@"\.(jpg|jpeg|png|gif|bmp|tiff|tif|webp|svg|ico)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex ImageFileRegex();

    /// <summary>
    /// 验证是否为视频文件扩展名
    /// </summary>
    [GeneratedRegex(@"\.(mp4|avi|mkv|mov|wmv|flv|webm|m4v|3gp|ts)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex VideoFileRegex();

    /// <summary>
    /// 验证是否为音频文件扩展名
    /// </summary>
    [GeneratedRegex(@"\.(mp3|wav|flac|aac|ogg|wma|m4a|opus)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex AudioFileRegex();

    /// <summary>
    /// 验证是否为文档文件扩展名
    /// </summary>
    [GeneratedRegex(@"\.(pdf|doc|docx|xls|xlsx|ppt|pptx|txt|rtf|odt|ods|odp)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex DocumentFileRegex();

    #endregion

    #region 网络相关验证

    /// <summary>
    /// 验证是否为IPv4地址
    /// </summary>
    [GeneratedRegex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", RegexOptions.Compiled)]
    public static partial Regex IpRegex();

    /// <summary>
    /// 验证是否为IPv6地址
    /// </summary>
    [GeneratedRegex(@"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^::1$|^::$", RegexOptions.Compiled)]
    public static partial Regex Ipv6Regex();

    /// <summary>
    /// 验证是否为MAC地址
    /// </summary>
    [GeneratedRegex(@"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$", RegexOptions.Compiled)]
    public static partial Regex MacAddressRegex();

    /// <summary>
    /// 验证是否为端口号
    /// </summary>
    [GeneratedRegex(@"^([1-9]|[1-9]\d{1,3}|[1-5]\d{4}|6[0-4]\d{3}|65[0-4]\d{2}|655[0-2]\d|6553[0-5])$", RegexOptions.Compiled)]
    public static partial Regex PortRegex();

    /// <summary>
    /// 验证是否为域名
    /// </summary>
    [GeneratedRegex(@"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$", RegexOptions.Compiled)]
    public static partial Regex DomainRegex();

    #endregion

    #region 编程相关验证

    /// <summary>
    /// 验证是否为合法的变量名（C#、Java等）
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled)]
    public static partial Regex VariableNameRegex();

    /// <summary>
    /// 验证是否为Base64编码
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9+/]*={0,2}$", RegexOptions.Compiled)]
    public static partial Regex Base64Regex();

    /// <summary>
    /// 验证是否为JSON格式
    /// </summary>
    [GeneratedRegex(@"^[\],:{}\s]*$", RegexOptions.Compiled)]
    public static partial Regex JsonFormatRegex();

    /// <summary>
    /// 验证是否为SQL注入攻击
    /// </summary>
    [GeneratedRegex(@"(\b(select|insert|update|delete|drop|create|alter|exec|execute|union|script)\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex SqlInjectionRegex();

    #endregion

    #region 时间日期验证

    /// <summary>
    /// 验证是否为月份
    /// </summary>
    [GeneratedRegex(@"^(0?[1-9]|1[0-2])$", RegexOptions.Compiled)]
    public static partial Regex MonthRegex();

    /// <summary>
    /// 验证是否为日期
    /// </summary>
    [GeneratedRegex(@"^((0?[1-9])|((1|2)[0-9])|30|31)$", RegexOptions.Compiled)]
    public static partial Regex DayRegex();

    /// <summary>
    /// 验证是否为Cron表达式（简化版，支持标准5字段格式）
    /// </summary>
    [GeneratedRegex(@"^\s*([0-9,\-\*/]+)\s+([0-9,\-\*/]+)\s+([0-9,\-\*/]+)\s+([0-9,\-\*/]+)\s+([0-6,\-\*/]+)\s*$", RegexOptions.Compiled)]
    public static partial Regex CronRegex();

    /// <summary>
    /// 验证是否为时间格式（HH:mm:ss）
    /// </summary>
    [GeneratedRegex(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$", RegexOptions.Compiled)]
    public static partial Regex TimeRegex();

    /// <summary>
    /// 验证是否为日期格式（yyyy-MM-dd）
    /// </summary>
    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$", RegexOptions.Compiled)]
    public static partial Regex DateRegex();

    /// <summary>
    /// 验证是否为日期时间格式（yyyy-MM-dd HH:mm:ss）
    /// </summary>
    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01]) ([01]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$", RegexOptions.Compiled)]
    public static partial Regex DateTimeRegex();

    /// <summary>
    /// 验证是否为ISO 8601日期时间格式
    /// </summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{3})?Z?$", RegexOptions.Compiled)]
    public static partial Regex Iso8601DateTimeRegex();

    #endregion
}

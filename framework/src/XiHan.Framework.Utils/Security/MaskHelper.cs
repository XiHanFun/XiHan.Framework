#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MaskHelper
// Guid:86738b17-6256-4f60-a9cd-016a7a471396
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/22 14:28:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.RegularExpressions;
using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.Security;

/// <summary>
/// 脱敏帮助类，提供常用敏感信息的脱敏处理方法
/// </summary>
public static partial class MaskHelper
{
    /// <summary>
    /// 通用脱敏方法：保留前面 frontCount 个字符和后面 endCount 个字符，其余部分用 maskChar 替换
    /// 如果待处理字符串的前后保留位数超过字符串长度，则按字符串长度脱敏<see cref="Mask(string, char)"/>"/>
    /// </summary>
    /// <param name="input">原始字符串</param>
    /// <param name="frontCount">保留前面字符数</param>
    /// <param name="endCount">保留后面字符数</param>
    /// <param name="maskChar">脱敏字符，默认使用星号*</param>
    /// <returns>脱敏后的字符串</returns>
    public static string Mask(this string input, int frontCount, int endCount, char maskChar = '*')
    {
        if (string.IsNullOrWhiteSpace(input?.Trim()))
        {
            return string.Empty;
        }

        var length = input.Length;
        if (frontCount + endCount >= length)
        {
            return Mask(input);
        }

        var maskLength = length - frontCount - endCount;
        return string.Concat(input.AsSpan(0, frontCount), new string(maskChar, maskLength), input.AsSpan(length - endCount, endCount));
    }

    /// <summary>
    /// 通用脱敏方法: 按字符串长度脱敏
    /// </summary>
    /// <param name="input">原始字符串</param>
    /// <param name="maskChar">脱敏字符，默认使用星号*</param>
    /// <returns></returns>
    public static string Mask(this string input, char maskChar = '*')
    {
        if (string.IsNullOrWhiteSpace(input?.Trim()))
        {
            return string.Empty;
        }

        input = input.Trim();
        var masks = maskChar.ToString().PadLeft(4, maskChar);
        return input.Length switch
        {
            >= 11 => Regex11().Replace(input, $"$1{masks}$2"),
            10 => Regex10().Replace(input, $"$1{masks}$2"),
            9 => Regex9().Replace(input, $"$1{masks}$2"),
            8 => Regex8().Replace(input, $"$1{masks}$2"),
            7 => Regex7().Replace(input, $"$1{masks}$2"),
            6 => Regex6().Replace(input, $"$1{masks}$2"),
            _ => RegexDefault().Replace(input, $"$1{masks}")
        };
    }

    /// <summary>
    /// 脱敏手机号：保留前三位和后四位，其余用星号替换
    /// 例如：13812345678 -> 138****5678
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <returns>脱敏后的手机号</returns>
    public static string MaskPhone(string phone)
    {
        return string.IsNullOrEmpty(phone) || phone.Length < 7 ? Mask(phone) : Mask(phone, 3, 4);
    }

    /// <summary>
    /// 脱敏身份证号：保留前四位和后四位，其余字符用星号替换
    /// 例如：11010119800101001X -> 1101***********1X
    /// </summary>
    /// <param name="idCard">身份证号</param>
    /// <returns>脱敏后的身份证号</returns>
    public static string MaskIdCard(string idCard)
    {
        return string.IsNullOrEmpty(idCard) || idCard.Length < 8 ? Mask(idCard) : Mask(idCard, 4, 4);
    }

    /// <summary>
    /// 脱敏银行卡号：保留前四位和后四位，其余用星号替换
    /// 例如：6222020200112233445 -> 6222********3445
    /// </summary>
    /// <param name="bankCard">银行卡号</param>
    /// <returns>脱敏后的银行卡号</returns>
    public static string MaskBankCard(string bankCard)
    {
        return string.IsNullOrEmpty(bankCard) || bankCard.Length < 8 ? Mask(bankCard) : Mask(bankCard, 4, 4);
    }

    /// <summary>
    /// 脱敏邮箱：保留邮箱前缀部分的前1-2个字符，其余部分用星号替换，再拼接邮箱域名
    /// 例如：test@example.com -> te**@example.com
    /// </summary>
    /// <param name="email">邮箱地址</param>
    /// <returns>脱敏后的邮箱地址</returns>
    public static string MaskEmail(string email)
    {
        var emailRegex = RegexHelper.EmailRegex();
        var match = emailRegex.Match(email);
        if (!match.Success)
        {
            return email;
        }
        var userName = match.Groups[1].Value;
        var domain = match.Groups[2].Value;
        var suffix = match.Groups[3].Value;

        if (userName.Length <= 3)
        {
            userName += new string('*', 3 - userName.Length);
        }
        else
        {
            userName = userName[..3] + new string('*', userName.Length - 3);
        }

        domain = domain[..3] + new string('*', domain.Length - 3);

        return $"{userName}@{domain}.{suffix}";
    }

    /// <summary>
    /// 脱敏中文姓名：
    /// 对于单字姓名直接返回；两个字的姓名保留第一个字，其余用*替换；
    /// 多字姓名保留第一个和最后一个字，中间部分用星号替换
    /// 例如：张三 -> 张*；欧阳娜娜 -> 欧**娜
    /// </summary>
    /// <param name="name">中文姓名</param>
    /// <returns>脱敏后的姓名</returns>
    public static string MaskChineseName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var length = name.Length;
        return length == 1
            ? name
            : length == 2
                ? string.Concat(name.AsSpan(0, 1), "*")
                : string.Concat(name.AsSpan(0, 1), new string('*', length - 2), name.AsSpan(length - 1, 1));
    }

    /// <summary>
    /// 脱敏地址信息：保留前6个字符和最后2个字符，其余用星号替换
    /// 例如：北京市朝阳区建国路1号 -> 北京市朝阳区****号
    /// 如果地址长度不足，则返回原地址
    /// </summary>
    /// <param name="address">地址信息</param>
    /// <returns>脱敏后的地址信息</returns>
    public static string MaskAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return address;
        }

        var length = address.Length;
        return length <= 8 ? address : Mask(address, 6, 2);
    }

    /// <summary>
    /// 脱敏密码：将密码全部用星号替换
    /// 返回与原密码相同长度的星号字符串
    /// </summary>
    /// <param name="password">密码</param>
    /// <returns>脱敏后的密码</returns>
    public static string MaskPassword(string password)
    {
        return string.IsNullOrEmpty(password) ? password : new string('*', password.Length);
    }

    /// <summary>
    /// 脱敏车牌号
    /// </summary>
    /// <param name="plate"></param>
    /// <returns></returns>
    public static string MaskLicensePlate(string plate)
    {
        return string.IsNullOrEmpty(plate) ? plate : plate.Length < 2 ? plate : Mask(plate, 2, 1);
    }

    /// <summary>
    /// 脱敏URL敏感参数
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static string MaskUrlParams(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        var regex = RegexHelper.RequestSecurityParamsRegex();
        return regex.Replace(url, "******");
    }

    /// <summary>
    /// 脱敏JSON
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public static string MaskJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        var patterns = new (string pattern, string replacement)[]
        {
            // 个人信息相关
            ($@"\""{GenerateCaseInsensitivePattern("name")}\""\s*:\s*\""([^\""]+)\""", $"\"name\": \"{MaskChineseName("$1")}\""),
            ($@"\""{GenerateCaseInsensitivePattern("phone")}\""\s*:\s*\""([^\""]+)\""", $"\"phone\": \"{MaskPhone("$1")}\""),
            ($@"\""{GenerateCaseInsensitivePattern("idCard")}\""\s*:\s*\""([^\""]+)\""", $"\"idCard\": \"{MaskIdCard("$1")}\""),
            ($@"\""{GenerateCaseInsensitivePattern("email")}\""\s*:\s*\""([^\""]+)\""", $"\"email\": \"{MaskEmail("$1")}\""),

            // 金融信息相关
            ($@"\""{GenerateCaseInsensitivePattern("bankCard")}\""\s*:\s*\""([^\""]+)\""", $"\"bankCard\": \"{MaskBankCard("$1")}\""),

            // 安全凭证相关
            ($@"\""{GenerateCaseInsensitivePattern("password")}\""\s*:\s*\""([^\""]+)\""", $"\"password\": \"{MaskPassword("$1")}\""),
            ($@"\""{GenerateCaseInsensitivePattern("otp")}\""\s*:\s*\""([^\""]+)\""", "\"otp\": \"******\""),
            ($@"\""{GenerateCaseInsensitivePattern("authorization")}\""\s*:\s*\""([^\""]+)\""", "\"authorization\": \"******\""),
            ($@"\""{GenerateCaseInsensitivePattern("token")}\""\s*:\s*\""([^\""]+)\""", "\"token\": \"******\""),

            // 地址和位置信息相关
            ($@"\""{GenerateCaseInsensitivePattern("address")}\""\s*:\s*\""([^\""]+)\""", $"\"address\": \"{MaskAddress("$1")}\""),
            ($@"\""{GenerateCaseInsensitivePattern("licensePlate")}\""\s*:\s*\""([^\""]+)\""", $"\"licensePlate\": \"{MaskLicensePlate("$1")}\""),

            // URL相关
            ($@"\""{GenerateCaseInsensitivePattern("url")}\""\s*:\s*\""([^\""]+)\""", $"\"url\": \"{MaskUrlParams("$1")}\"")
        };

        foreach (var (pattern, replacement) in patterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            json = regex.Replace(json, replacement);
        }

        return json;
    }

    /// <summary>
    /// 生成大小写不敏感的正则表达式模式
    /// </summary>
    /// <param name="word">要生成模式的单词</param>
    /// <returns>大小写不敏感的正则表达式模式</returns>
    private static string GenerateCaseInsensitivePattern(string word)
    {
        return string.Join("", word.Select(c => $"[{char.ToLower(c)}{char.ToUpper(c)}]"));
    }

    [GeneratedRegex("(.{3}).*(.{4})")]
    private static partial Regex Regex11();

    [GeneratedRegex("(.{3}).*(.{3})")]
    private static partial Regex Regex10();

    [GeneratedRegex("(.{2}).*(.{3})")]
    private static partial Regex Regex9();

    [GeneratedRegex("(.{2}).*(.{2})")]
    private static partial Regex Regex8();

    [GeneratedRegex("(.{1}).*(.{2})")]
    private static partial Regex Regex7();

    [GeneratedRegex("(.{1}).*(.{1})")]
    private static partial Regex Regex6();

    [GeneratedRegex("(.{1}).*")]
    private static partial Regex RegexDefault();
}

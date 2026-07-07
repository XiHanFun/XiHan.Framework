#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogSanitizer
// Guid:8c1f4f4e-1c1c-4b7e-9a3a-0f2f4f6a9d21
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/11 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.RegularExpressions;

namespace XiHan.Framework.Auditing;

/// <summary>
/// 日志敏感数据脱敏器
/// </summary>
/// <remarks>
/// 用于请求体 / 请求参数 / 查询串在落日志前的统一脱敏：
/// <list type="bullet">
///   <item>键名命中敏感词（密码、令牌、密钥、验证码、银行卡、身份证等）时整体掩码其值；</item>
///   <item>值命中身份证号特征（15/18 位含出生日期段）时按模式掩码；</item>
///   <item>脱敏仅作用于日志副本，不影响业务管道中的原始请求。</item>
/// </list>
/// </remarks>
public static partial class LogSanitizer
{
    /// <summary>
    /// 掩码占位符
    /// </summary>
    public const string Mask = "***";

    /// <summary>
    /// 敏感键名（小写比对，键名包含任一关键字即命中）
    /// </summary>
    private const string SensitiveKeyPattern =
        "password|passwd|pwd|secret|token|credential|authorization|otp" +
        "|verifycode|verificationcode|twofactorcode" +
        "|bankcard|cardno|cardnumber|accountno" +
        "|idcard|identitycard|idnumber";

    /// <summary>
    /// 对 JSON / 表单文本做敏感数据脱敏
    /// </summary>
    /// <param name="content">原始文本（JSON 请求体、序列化参数等）</param>
    /// <returns>脱敏后的文本；输入为空时原样返回</returns>
    public static string? MaskSensitiveData(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        // 1) JSON 键值对："password": "xxx" => "password": "***"
        var masked = SensitiveJsonPairRegex().Replace(content, m => $"{m.Groups["prefix"].Value}\"{Mask}\"");

        // 2) 表单/查询风格键值对：password=xxx => password=***
        masked = SensitiveFormPairRegex().Replace(masked, m => $"{m.Groups["key"].Value}={Mask}");

        // 3) 身份证号模式（18 位含出生日期段 / 15 位旧证）
        masked = IdCardRegex().Replace(masked, static m => MaskMiddle(m.Value));

        return masked;
    }

    /// <summary>
    /// 对 URL 查询串做敏感数据脱敏（仅处理键值，不改变结构）
    /// </summary>
    /// <param name="queryString">原始查询串（可含开头的 ?）</param>
    /// <returns>脱敏后的查询串</returns>
    public static string? MaskQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return queryString;
        }

        var masked = SensitiveFormPairRegex().Replace(queryString, m => $"{m.Groups["key"].Value}={Mask}");
        return IdCardRegex().Replace(masked, static m => MaskMiddle(m.Value));
    }

    /// <summary>
    /// 保留首尾、掩码中段（用于身份证等需要保留可辨识度的场景）
    /// </summary>
    private static string MaskMiddle(string value)
    {
        return value.Length <= 6 ? Mask : $"{value[..3]}{Mask}{value[^3..]}";
    }

    /// <summary>
    /// 敏感键 JSON 键值对（prefix 捕获键与冒号，值部分整体替换）
    /// </summary>
    [GeneratedRegex(
        "(?<prefix>\"[^\"]*(?:" + SensitiveKeyPattern + ")[^\"]*\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveJsonPairRegex();

    /// <summary>
    /// 敏感键表单/查询键值对
    /// </summary>
    [GeneratedRegex(
        "(?<key>[?&]?[^?&=\\s\"]*(?:" + SensitiveKeyPattern + ")[^?&=\\s\"]*)=[^&\\s\"]*",
        RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveFormPairRegex();

    /// <summary>
    /// 身份证号（18 位：6 位地区 + 8 位出生日期 + 3 位顺序 + 校验位；15 位旧证）
    /// </summary>
    [GeneratedRegex(
        @"(?<!\d)(?:\d{6}(?:19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3}[\dXx]|\d{6}\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3})(?!\d)")]
    private static partial Regex IdCardRegex();
}

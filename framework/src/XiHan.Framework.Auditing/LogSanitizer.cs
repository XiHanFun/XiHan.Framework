// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Auditing;

/// <summary>
/// 日志敏感数据脱敏器
/// </summary>
/// <remarks>
/// 用于请求体 / 请求参数 / 查询串 / 请求头在落日志前的统一脱敏：
/// <list type="bullet">
///   <item>键名命中敏感词（密码、令牌、密钥、验证码、银行卡、身份证等）时整体掩码其值；</item>
///   <item>值命中身份证号特征（15/18 位含出生日期段）时按模式掩码；</item>
///   <item>请求头按头名命中敏感词（Authorization、Cookie、X-Api-Key、签名等）时整体掩码其值；</item>
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
    /// 敏感名关键字（<b>唯一</b>的敏感判定来源：请求体键、查询串键、请求头名、实体字段/列名共用）
    /// </summary>
    /// <remarks>
    /// 比对前先归一化（去掉分隔符 + 转小写），使 <c>X-Api-Key</c> → <c>xapikey</c>、
    /// <c>Connection_String</c> → <c>connectionstring</c>、<c>apiKey</c> → <c>apikey</c> 都能命中同一份规则。
    /// 命中即<b>整体掩码其值</b>——<c>Authorization: Bearer &lt;JWT&gt;</c>、密码哈希这类值一旦落库即等同凭证泄露，保留片段无意义。
    /// <para>
    /// 不含裸 <c>otp</c>：归一化去掉分隔符后 <c>Not_Processed</c> → <c>notprocessed</c> 会含 <c>otp</c> 而被误伤，故改用明确的 OTP 命名。
    /// </para>
    /// </remarks>
    private const string SensitiveNamePattern =
        "password|passwd|pwd|secret|token|credential|authorization" +
        "|otpcode|onetimepassword|twofactor|verifycode|verificationcode" +
        "|bankcard|cardno|cardnumber|accountno" +
        "|idcard|identitycard|idnumber" +
        "|cookie|apikey|accesskey|privatekey|connectionstring|salt|signature|session|recoverycode";

    /// <summary>
    /// 元数据后缀：命中敏感词、但以这些结尾的名字是「关于秘密的元数据」而非秘密本身，<b>不得掩码</b>
    /// </summary>
    /// <remarks>
    /// 反例才是重点：<c>Last_Password_Change_Time</c>（改密时间）、<c>Password_Expiration_Time</c>、
    /// <c>Token_Type</c>（"Bearer"）、<c>Access_Token_Lifetime</c>、<c>Signature_Type</c>、<c>Max_Output_Tokens</c>（数字 4096）——
    /// 这些恰恰是审计最需要看的东西，掩掉它们等于把审计价值一起掩掉。
    /// 「宁可多掩」只在秘密本身上成立，对秘密的元数据不成立。
    /// </remarks>
    private const string MetadataSuffixPattern =
        "time|date|lifetime|expiration|expires|expiry|type|kind" +
        "|count|total|length|size|status|state|enabled|disabled" +
        "|algorithm|version|mode|format|policy|strategy|attempts|tokens";

    /// <summary>
    /// 对 JSON / 表单文本做敏感数据脱敏（键名命中即掩其值）
    /// </summary>
    /// <param name="content">原始文本（JSON 请求体、序列化参数等）</param>
    /// <returns>脱敏后的文本；输入为空时原样返回</returns>
    public static string? MaskSensitiveData(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        // 1) JSON 键值对（值可为字符串/数字/布尔/null）："apiKey": "sk-xxx" => "apiKey":"***"
        var masked = JsonPairRegex().Replace(content, static m =>
        {
            var key = m.Groups["key"].Value;
            return IsSensitiveName(key) ? $"\"{key}\":\"{Mask}\"" : m.Value;
        });

        // 2) 表单/查询风格键值对：password=xxx => password=***
        masked = FormPairRegex().Replace(masked, static m =>
        {
            var key = m.Groups["key"].Value;
            return IsSensitiveName(key) ? $"{m.Groups["prefix"].Value}{key}={Mask}" : m.Value;
        });

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

        var masked = FormPairRegex().Replace(queryString, static m =>
        {
            var key = m.Groups["key"].Value;
            return IsSensitiveName(key) ? $"{m.Groups["prefix"].Value}{key}={Mask}" : m.Value;
        });

        return IdCardRegex().Replace(masked, static m => MaskMiddle(m.Value));
    }

    /// <summary>
    /// 判断某个名字（请求体/查询串的键、请求头名、实体字段名、数据库列名）是否敏感
    /// </summary>
    /// <remarks>
    /// 全框架敏感判定的<b>唯一入口</b>。两步：归一化后命中 <see cref="SensitiveNamePattern"/>，
    /// 且<b>不</b>以 <see cref="MetadataSuffixPattern"/> 结尾——后者把「关于秘密的元数据」摘出去
    /// （<c>Last_Password_Change_Time</c>、<c>Token_Type</c>、<c>Max_Output_Tokens</c> 不是秘密，掩了反而丢审计线索）。
    /// </remarks>
    /// <param name="name">待判定的名字，比对前会归一化（去分隔符 + 转小写）</param>
    /// <returns>敏感返回 true</returns>
    public static bool IsSensitiveName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = NormalizeName(name);
        return SensitiveNameRegex().IsMatch(normalized) && !MetadataSuffixRegex().IsMatch(normalized);
    }

    /// <summary>
    /// 字段级脱敏：名字敏感则整体掩码其值，否则原样返回
    /// </summary>
    /// <param name="name">字段名 / 列名</param>
    /// <param name="value">字段值</param>
    /// <returns>敏感字段返回 <see cref="Mask"/>，否则返回原值</returns>
    public static object? MaskFieldValue(string? name, object? value)
    {
        return IsSensitiveName(name) ? Mask : value;
    }

    /// <summary>
    /// 对「键即字段名」的 JSON 对象做<b>字段级</b>脱敏：敏感键的值整体替换为掩码
    /// </summary>
    /// <remarks>
    /// 与 <see cref="MaskSensitiveData"/> 的区别：后者靠正则只能掩<b>字符串字面量</b>的值，
    /// 本方法解析 JSON，敏感键无论值是字符串、数字、null 还是对象都能掩掉——用于实体前后快照（列名即键）。
    /// 解析失败时回落到正则脱敏，绝不原样吐出。
    /// </remarks>
    /// <param name="json">JSON 对象文本（形如 <c>{"Password":"x","Name":"y"}</c>）</param>
    /// <returns>脱敏后的 JSON；输入为空时原样返回</returns>
    public static string? MaskJsonFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return MaskSensitiveData(json);
            }

            var masked = new Dictionary<string, object?>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                masked[property.Name] = IsSensitiveName(property.Name)
                    ? Mask
                    : property.Value.Clone();
            }

            return JsonSerializer.Serialize(masked);
        }
        catch (JsonException)
        {
            return MaskSensitiveData(json);
        }
    }

    /// <summary>
    /// 对请求头做脱敏：头名命中敏感词的整体掩码其值，其余头的值再走一遍通用脱敏
    /// </summary>
    /// <param name="headers">原始请求头</param>
    /// <returns>脱敏后的头字典（大小写不敏感）；输入为空时返回空字典</returns>
    public static Dictionary<string, string?> MaskHeaders(IEnumerable<KeyValuePair<string, string?>>? headers)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
        {
            return result;
        }

        foreach (var header in headers)
        {
            result[header.Key] = IsSensitiveName(header.Key) ? Mask : MaskSensitiveData(header.Value);
        }

        return result;
    }

    /// <summary>
    /// 保留首尾、掩码中段（用于身份证等需要保留可辨识度的场景）
    /// </summary>
    private static string MaskMiddle(string value)
    {
        return value.Length <= 6 ? Mask : $"{value[..3]}{Mask}{value[^3..]}";
    }

    /// <summary>
    /// 名字归一化：只保留字母数字并转小写，使 <c>X-Api-Key</c> / <c>Connection_String</c> 这类分隔命名可被关键字命中
    /// </summary>
    private static string NormalizeName(string name)
    {
        return string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    /// <summary>
    /// 敏感名（归一化后包含任一关键字即命中）
    /// </summary>
    [GeneratedRegex("(?:" + SensitiveNamePattern + ")", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveNameRegex();

    /// <summary>
    /// 元数据后缀（归一化后以此结尾 → 是关于秘密的元数据，不掩）
    /// </summary>
    [GeneratedRegex("(?:" + MetadataSuffixPattern + ")$", RegexOptions.IgnoreCase)]
    private static partial Regex MetadataSuffixRegex();

    /// <summary>
    /// JSON 键值对（捕获键名，值可为字符串/数字/布尔/null）；是否掩码交由 <see cref="IsSensitiveName"/> 判定
    /// </summary>
    [GeneratedRegex(
        "\"(?<key>[^\"]+)\"\\s*:\\s*(?:\"(?:[^\"\\\\]|\\\\.)*\"|-?\\d+(?:\\.\\d+)?|true|false|null)",
        RegexOptions.IgnoreCase)]
    private static partial Regex JsonPairRegex();

    /// <summary>
    /// 表单/查询键值对（捕获键名）；是否掩码交由 <see cref="IsSensitiveName"/> 判定
    /// </summary>
    [GeneratedRegex(
        "(?<prefix>[?&]?)(?<key>[^?&=\\s\"]+)=(?<value>[^&\\s\"]*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex FormPairRegex();

    /// <summary>
    /// 身份证号（18 位：6 位地区 + 8 位出生日期 + 3 位顺序 + 校验位；15 位旧证）
    /// </summary>
    [GeneratedRegex(
        @"(?<!\d)(?:\d{6}(?:19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3}[\dXx]|\d{6}\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3})(?!\d)")]
    private static partial Regex IdCardRegex();
}

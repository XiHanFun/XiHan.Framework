#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorObfuscationOptions
// Guid:0d1e2f3a-5b6c-7d8e-9f0a-1b2c3d4e5f6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Options;

/// <summary>
/// 错误混淆配置选项
/// </summary>
public class ErrorObfuscationOptions
{
    /// <summary>
    /// 创建默认配置（完全随机）
    /// </summary>
    public static ErrorObfuscationOptions Default => new();

    /// <summary>
    /// 指定编程语言，null 表示随机
    /// </summary>
    public ProgrammingLanguage? Language { get; set; }

    /// <summary>
    /// 指定错误格式，null 表示随机
    /// </summary>
    public ErrorFormat? Format { get; set; }

    /// <summary>
    /// 指定HTTP状态码，0 表示随机
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 固定延迟时间（毫秒），0 表示不延迟
    /// </summary>
    public int DelayMs { get; set; }

    /// <summary>
    /// 是否使用随机延迟
    /// </summary>
    public bool RandomDelay { get; set; }

    /// <summary>
    /// 随机延迟的最小值（毫秒），默认 100ms
    /// </summary>
    public int MinDelayMs { get; set; } = 100;

    /// <summary>
    /// 随机延迟的最大值（毫秒），默认 2000ms
    /// </summary>
    public int MaxDelayMs { get; set; } = 2000;

    /// <summary>
    /// 创建带随机延迟的配置
    /// </summary>
    public static ErrorObfuscationOptions WithRandomDelay(int minMs = 100, int maxMs = 2000) => new()
    {
        RandomDelay = true,
        MinDelayMs = minMs,
        MaxDelayMs = maxMs
    };

    /// <summary>
    /// 创建指定格式的配置
    /// </summary>
    public static ErrorObfuscationOptions WithFormat(ErrorFormat format) => new()
    {
        Format = format
    };

    /// <summary>
    /// 创建指定语言的配置
    /// </summary>
    public static ErrorObfuscationOptions WithLanguage(ProgrammingLanguage language) => new()
    {
        Language = language
    };

    /// <summary>
    /// 创建指定语言和格式的配置
    /// </summary>
    public static ErrorObfuscationOptions With(ProgrammingLanguage language, ErrorFormat format) => new()
    {
        Language = language,
        Format = format
    };
}

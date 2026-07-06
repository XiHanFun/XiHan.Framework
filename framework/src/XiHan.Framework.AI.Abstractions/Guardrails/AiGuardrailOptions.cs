#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AiGuardrailOptions
// Guid:a1b2c3d4-e5f6-4a22-9c22-0a0b0c0d0e22
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Guardrails;

/// <summary>
/// 护栏配置（appsettings 的 XiHan:AI:Guardrail 节）
/// </summary>
public sealed class AiGuardrailOptions
{
    /// <summary>
    /// 配置节名
    /// </summary>
    public const string SectionName = "XiHan:AI:Guardrail";

    /// <summary>
    /// 敏感词黑名单（大小写不敏感的子串匹配，命中即拦截）
    /// </summary>
    public IList<string> BlockedKeywords { get; set; } = [];

    /// <summary>
    /// 自定义注入检测正则（大小写不敏感，命中即拦截）
    /// </summary>
    public IList<string> InjectionPatterns { get; set; } = [];

    /// <summary>
    /// 是否启用内置提示注入启发式正则
    /// </summary>
    public bool UseBuiltInInjectionHeuristics { get; set; } = true;

    /// <summary>
    /// 拦截时返回的拒绝话术
    /// </summary>
    public string RefusalMessage { get; set; } = "抱歉，你的请求包含不被允许的内容，已被安全策略拦截。";
}

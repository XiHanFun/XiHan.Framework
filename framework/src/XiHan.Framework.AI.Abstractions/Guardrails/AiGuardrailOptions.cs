// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

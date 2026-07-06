#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIServiceCollectionExtensions
// Guid:b2c3d4e5-f6a7-4b05-9d05-1a1b1c1d1e05
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.AI.Abstractions.Agents;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Configuration;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Skills;
using XiHan.Framework.AI.Agents;
using XiHan.Framework.AI.Chat;
using XiHan.Framework.AI.Configuration;
using XiHan.Framework.AI.Guardrails;
using XiHan.Framework.AI.Providers;
using XiHan.Framework.AI.Skills;

namespace XiHan.Framework.AI.Extensions.DependencyInjection;

/// <summary>
/// XiHan AI 服务注册扩展
/// </summary>
public static class XiHanAIServiceCollectionExtensions
{
    /// <summary>
    /// 注册 XiHan AI 会话能力（provider 配置源、解析器、会话门面）
    /// </summary>
    /// <remarks>
    /// 全部用 <c>TryAddSingleton</c> 注册,应用层可用 SysAiProvider 的 DB 配置源等自定义实现覆盖默认项。
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanAI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<XiHanAiOptions>().BindConfiguration(XiHanAiOptions.SectionName);
        services.AddOptions<AiGuardrailOptions>().BindConfiguration(AiGuardrailOptions.SectionName);

        // provider 配置源:默认读 Options（appsettings 兜底），应用层可换 DB 源
        services.TryAddSingleton<IAiProviderConfigStore, OptionsAiProviderConfigStore>();

        // 默认护栏（敏感词/注入黑名单；应用可 AddSingleton<IAiGuardrail,X> 追加，全部放行才通过）
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAiGuardrail, KeywordBlocklistGuardrail>());

        // OpenAI 兼容工厂 + 多 provider 解析器（含 IChatClient 缓存）
        services.TryAddSingleton<OpenAiCompatibleChatClientFactory>();
        services.TryAddSingleton<IAiChatClientResolver, AiChatClientResolver>();

        // 嵌入工厂 + 解析器（RAG 用；含 IEmbeddingGenerator 缓存）
        services.TryAddSingleton<OpenAiEmbeddingGeneratorFactory>();
        services.TryAddSingleton<IAiEmbeddingGeneratorResolver, AiEmbeddingGeneratorResolver>();

        // 会话门面
        services.TryAddSingleton<IXiHanAiService, XiHanAiService>();

        // 提示词库:默认读 Options（appsettings 兜底），应用层可换 DB 源
        services.TryAddSingleton<IAiPromptStore, OptionsAiPromptStore>();

        // 技能注册表（收纳应用注册的 IAiSkill）+ Agent 工厂（MAF）
        services.TryAddSingleton<IAiSkillRegistry, DefaultAiSkillRegistry>();
        services.TryAddSingleton<IXiHanAgentFactory, XiHanAgentFactory>();

        return services;
    }
}

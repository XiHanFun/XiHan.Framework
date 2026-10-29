// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.AI.Abstractions.Chat;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// AI 子系统的服务登记。
/// </summary>
/// <remarks>
/// 所有 AI 特性共用的 <see cref="AddAI(IServiceCollection, IConfiguration)"/>（幂等），以及工厂、护栏、提示词与工具的登记。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class AIServiceCollectionExtensions
{
    /// <summary>
    /// 按配置登记 AI 核心服务。
    /// </summary>
    public static IServiceCollection AddAI(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddAI(AIOptions.FromConfiguration(configuration));
    }

    /// <summary>
    /// 登记 AI 核心服务。
    /// </summary>
    /// <remarks>
    /// 选项、消息目录、Provider 目录、聊天入口、提示词目录、工具目录、知识库流水线；已登记的不重复。
    /// </remarks>
    public static IServiceCollection AddAI(this IServiceCollection services, AIOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddMessageCatalog(AIMessages.Catalog);
        services.TryAddSingleton(options);
        services.TryAddSingleton(options.Guardrails);
        services.TryAddSingleton<IAIProviderCatalog, AIProviderCatalog>();
        services.TryAddSingleton<IAIPromptCatalog, AIPromptCatalog>();
        services.TryAddSingleton<IAIToolCatalog, AIToolCatalog>();
        services.TryAddScoped<IAIChat, AIChat>();
        services.TryAddSingleton<IChunkingStrategy, FixedWindowChunkingStrategy>();
        services.TryAddSingleton<IKnowledgeIngestor, KnowledgeIngestor>();
        services.TryAddSingleton<IKnowledgeRetriever, KnowledgeRetriever>();
        services.TryAddSingleton<IRagPromptAugmenter, RagPromptAugmenter>();
        if (options.Guardrails.Enabled)
        {
            services.AddAIGuardrail<KeywordBlocklistGuardrail>();
        }

        return services;
    }

    /// <summary>
    /// 登记聊天客户端工厂（<c>ai.chat@1</c> 的 Provider）。
    /// </summary>
    public static IServiceCollection AddAIChatClientFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFactory>(this IServiceCollection services) where TFactory : class, IChatClientFactory
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IChatClientFactory, TFactory>());
        return services;
    }

    /// <summary>
    /// 登记嵌入生成器工厂（<c>ai.embedding@1</c> 的 Provider）。
    /// </summary>
    public static IServiceCollection AddAIEmbeddingGeneratorFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFactory>(this IServiceCollection services) where TFactory : class, IEmbeddingGeneratorFactory
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEmbeddingGeneratorFactory, TFactory>());
        return services;
    }

    /// <summary>
    /// 登记护栏。
    /// </summary>
    /// <remarks>
    /// 按登记顺序执行。
    /// </remarks>
    public static IServiceCollection AddAIGuardrail<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TGuardrail>(this IServiceCollection services) where TGuardrail : class, IAIGuardrail
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIGuardrail, TGuardrail>());
        return services;
    }

    /// <summary>
    /// 登记提示词模板。
    /// </summary>
    /// <remarks>
    /// 同名覆盖配置里的。
    /// </remarks>
    public static IServiceCollection AddAIPrompt(this IServiceCollection services, AIPromptTemplate template)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(template);
        services.AddSingleton(template);
        return services;
    }

    /// <summary>
    /// 登记一个工具（<see cref="AIFunctionFactory"/> 创建的函数或任何 <see cref="AITool"/>）。
    /// </summary>
    public static IServiceCollection AddAITool(this IServiceCollection services, AITool tool)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tool);
        services.AddSingleton(tool);
        return services;
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.AI.Abstractions.Rag;
using XiHan.Framework.AI.Rag;

namespace XiHan.Framework.AI.Extensions.DependencyInjection;

/// <summary>
/// XiHan RAG 服务注册扩展
/// </summary>
public static class XiHanRAGServiceCollectionExtensions
{
    /// <summary>
    /// 注册 XiHan RAG 能力（切片/摄取/检索/提示增强的默认实现）
    /// </summary>
    /// <remarks>
    /// 不注册具体 <c>VectorStore</c>——连接器选择属部署事项，由应用层登记（如 <c>AddQdrantVectorStore</c>）。
    /// 全部 <c>TryAdd</c>，应用层可覆盖任一默认实现。
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanRAG(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IChunkingStrategy, FixedWindowChunkingStrategy>();
        services.TryAddSingleton<IKnowledgeIngestor, DefaultKnowledgeIngestor>();
        services.TryAddSingleton<IKnowledgeRetriever, DefaultKnowledgeRetriever>();
        services.TryAddSingleton<IRagPromptAugmenter, DefaultRagPromptAugmenter>();

        return services;
    }
}

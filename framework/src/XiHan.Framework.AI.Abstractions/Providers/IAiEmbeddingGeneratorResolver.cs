// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// 多 provider 嵌入生成器解析：按名取已构建好的 <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>
/// </summary>
/// <remarks>
/// 与 <c>IAiChatClientResolver</c> 同构：从 <c>IAiProviderConfigStore</c> 读配置，用 OpenAI 兼容适配器构建
/// <see cref="IEmbeddingGenerator{String, Embedding}"/>（模型取 <c>AiProviderOptions.EmbeddingModel</c>），按 provider 名缓存。
/// </remarks>
public interface IAiEmbeddingGeneratorResolver
{
    /// <summary>
    /// 解析指定 provider 的嵌入生成器（null 取默认 provider）
    /// </summary>
    IEmbeddingGenerator<string, Embedding<float>> Resolve(string? providerName = null);

    /// <summary>
    /// 使已缓存的嵌入生成器失效（下次 <see cref="Resolve"/> 按最新配置重建）
    /// </summary>
    /// <remarks>配置源改动 provider 的 key/baseUrl/embeddingModel 后调用，实现热切换。</remarks>
    void Invalidate(string? providerName = null);
}

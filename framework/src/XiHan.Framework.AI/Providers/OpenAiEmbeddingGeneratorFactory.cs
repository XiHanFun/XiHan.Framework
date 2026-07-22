// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;
using XiHan.Framework.AI.Abstractions.Configuration;

namespace XiHan.Framework.AI.Providers;

/// <summary>
/// OpenAI 兼容嵌入生成器工厂
/// </summary>
/// <remarks>
/// 与会话工厂同构：OpenAI/DeepSeek/Azure/vLLM/自训模型皆走 OpenAI 兼容协议——
/// 复用同一 <see cref="AiProviderOptions.ApiKey"/>/<see cref="AiProviderOptions.BaseUrl"/>，
/// 仅模型取 <see cref="AiProviderOptions.EmbeddingModel"/>。
/// </remarks>
public sealed class OpenAiEmbeddingGeneratorFactory
{
    /// <summary>
    /// 由配置构建嵌入生成器
    /// </summary>
    public IEmbeddingGenerator<string, Embedding<float>> Create(AiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            throw new InvalidOperationException($"AI Provider [{options.Provider}] 未配置嵌入模型（EmbeddingModel），无法用于 RAG 嵌入。");
        }

        // 本地/兼容端点常不校验 key，用占位符避免 ApiKeyCredential 空值报错；云端用真实 key
        var credential = new ApiKeyCredential(string.IsNullOrWhiteSpace(options.ApiKey) ? "no-key" : options.ApiKey);

        var embeddingClient = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? new EmbeddingClient(options.EmbeddingModel, credential)
            : new EmbeddingClient(options.EmbeddingModel, credential, new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) });

        return embeddingClient.AsIEmbeddingGenerator();
    }
}

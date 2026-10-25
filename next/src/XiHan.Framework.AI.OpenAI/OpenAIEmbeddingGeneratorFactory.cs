// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.OpenAI;

/// <summary>
/// <c>Kind = openai</c> 的嵌入生成器工厂：<c>OpenAIClient.GetEmbeddingClient(EmbeddingModel).AsIEmbeddingGenerator(EmbeddingDimensions)</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OpenAIEmbeddingGeneratorFactory : IEmbeddingGeneratorFactory
{
    /// <summary>
    /// 客户端工厂。
    /// </summary>
    private readonly OpenAIClientFactory _clients;

    /// <summary>
    /// 创建。
    /// </summary>
    public OpenAIEmbeddingGeneratorFactory(IHttpClientFactory? httpClients = null)
    {
        _clients = new OpenAIClientFactory(httpClients);
    }

    /// <inheritdoc />
    public string Kind => OpenAIChatClientFactory.KindName;

    /// <inheritdoc />
    public Result<IEmbeddingGenerator<string, Embedding<float>>> Create(AIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            return Error.Validation(AIErrors.RequestInvalid, AIMessages.ProviderInvalid, MessageArguments.Of(("name", options.Name), ("reason", "EmbeddingModel is required")));
        }

        return Result.Success(_clients.Create(options).GetEmbeddingClient(options.EmbeddingModel).AsIEmbeddingGenerator(options.EmbeddingDimensions));
    }
}

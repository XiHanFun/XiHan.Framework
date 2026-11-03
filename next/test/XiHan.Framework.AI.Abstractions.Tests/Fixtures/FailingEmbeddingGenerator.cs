// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 每次生成都抛出的嵌入生成器，模拟模型端点不可达
/// </summary>
internal sealed class FailingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        => throw new HttpRequestException("The embedding endpoint is unreachable.");

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

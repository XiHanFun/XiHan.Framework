// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Conformance.Embeddings;

/// <summary>
/// <c>Kind = hashing</c> 的嵌入工厂：维度取 <see cref="AIProviderOptions.EmbeddingDimensions"/>，默认 64。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HashingEmbeddingGeneratorFactory : IEmbeddingGeneratorFactory
{
    /// <summary>
    /// 种类。
    /// </summary>
    public const string KindName = "hashing";

    /// <summary>
    /// 创建过的生成器（供测试断言缓存与失效）。
    /// </summary>
    public List<HashingEmbeddingGenerator> Created { get; } = [];

    /// <inheritdoc />
    public string Kind => KindName;

    /// <inheritdoc />
    public Result<IEmbeddingGenerator<string, Embedding<float>>> Create(AIProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var generator = new HashingEmbeddingGenerator(options.EmbeddingDimensions ?? HashingEmbeddingGenerator.DefaultDimensions);
        Created.Add(generator);
        return Result.Success<IEmbeddingGenerator<string, Embedding<float>>>(generator);
    }
}

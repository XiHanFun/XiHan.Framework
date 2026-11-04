// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 种类 <c>failing</c> 的嵌入工厂：建出的生成器每次都抛出
/// </summary>
internal sealed class FailingEmbeddingGeneratorFactory : IEmbeddingGeneratorFactory
{
    /// <summary>
    /// 种类名
    /// </summary>
    public const string KindName = "failing";

    /// <inheritdoc />
    public string Kind => KindName;

    /// <inheritdoc />
    public Result<IEmbeddingGenerator<string, Embedding<float>>> Create(AIProviderOptions options)
        => Result.Success<IEmbeddingGenerator<string, Embedding<float>>>(new FailingEmbeddingGenerator());
}

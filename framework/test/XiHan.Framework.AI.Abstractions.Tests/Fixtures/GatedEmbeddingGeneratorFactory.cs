// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// <c>Kind = gated</c> 的嵌入工厂：按 Provider 的时限建 <see cref="GatedEmbeddingGenerator"/>
/// </summary>
internal sealed class GatedEmbeddingGeneratorFactory : IEmbeddingGeneratorFactory
{
    /// <summary>
    /// 时间源
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 闸门
    /// </summary>
    private readonly ProviderGate _gate;

    /// <summary>
    /// 创建
    /// </summary>
    public GatedEmbeddingGeneratorFactory(TimeProvider timeProvider, ProviderGate gate)
    {
        _timeProvider = timeProvider;
        _gate = gate;
    }

    /// <inheritdoc />
    public string Kind => GatedChatClientFactory.KindName;

    /// <inheritdoc />
    public Result<IEmbeddingGenerator<string, Embedding<float>>> Create(AIProviderOptions options)
        => Result.Success<IEmbeddingGenerator<string, Embedding<float>>>(new GatedEmbeddingGenerator(_timeProvider, options.Timeout, _gate));
}

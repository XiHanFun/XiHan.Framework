// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// 带自己传输超时、在闸门处停住再观察令牌的嵌入生成器。
/// </summary>
/// <remarks>
/// 与 <see cref="GatedChatClient"/> 同形：抛出时先看交给它的令牌、再看自己的超时；不抛出时接住取消，交回一条嵌入都没有的结果。
/// </remarks>
internal sealed class GatedEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>
    /// 嵌入维度。
    /// </summary>
    public const int Dimensions = 4;

    /// <summary>
    /// 时间源。
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 自己的传输超时。
    /// </summary>
    private readonly TimeSpan _timeout;

    /// <summary>
    /// 闸门。
    /// </summary>
    private readonly ProviderGate _gate;

    /// <summary>
    /// 创建。
    /// </summary>
    public GatedEmbeddingGenerator(TimeProvider timeProvider, TimeSpan timeout, ProviderGate gate)
    {
        _timeProvider = timeProvider;
        _timeout = timeout;
        _gate = gate;
    }

    /// <inheritdoc />
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var own = new CancellationTokenSource(_timeout, _timeProvider);
        await _gate.PassAsync();
        if (_gate.Throws)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (own.IsCancellationRequested)
            {
                throw new TaskCanceledException("The operation was cancelled because it exceeded the configured timeout.", new TimeoutException(), own.Token);
            }
        }

        // 接住取消：一条嵌入都不交回
        return cancellationToken.IsCancellationRequested || own.IsCancellationRequested
            ? []
            : [.. values.Select(static _ => new Embedding<float>(new float[Dimensions]))];
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

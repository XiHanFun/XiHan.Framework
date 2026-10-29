// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Conformance.Providers;

/// <summary>
/// Provider 目录套件的夹具。
/// </summary>
/// <remarks>
/// 装配了被测 Provider 的根服务、它的 Provider 名与嵌入维度，以及释放句柄。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AIProviderHost : IAsyncDisposable
{
    /// <summary>
    /// 释放句柄。
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 创建。
    /// </summary>
    public AIProviderHost(IServiceProvider services, string providerName, int embeddingDimensions, IAsyncDisposable host)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ProviderName = providerName;
        ArgumentOutOfRangeException.ThrowIfLessThan(embeddingDimensions, 1);
        EmbeddingDimensions = embeddingDimensions;
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 根服务。
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 被测 Provider 名。
    /// </summary>
    public string ProviderName { get; }

    /// <summary>
    /// 嵌入维度。
    /// </summary>
    public int EmbeddingDimensions { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}

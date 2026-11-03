// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Conformance.Providers;

/// <summary>
/// Provider 目录套件的夹具
/// </summary>
/// <remarks>
/// 装配了被测 Provider 的根服务、它的 Provider 名与嵌入维度、聊天与嵌入分开设默认时的默认嵌入 Provider 名，以及释放句柄
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AIProviderHost : IAsyncDisposable
{
    /// <summary>
    /// 释放句柄
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 创建
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
    /// 根服务
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 被测 Provider 名
    /// </summary>
    /// <remarks>
    /// 有默认的夹具里它也是默认聊天 Provider
    /// </remarks>
    public string ProviderName { get; }

    /// <summary>
    /// 嵌入维度
    /// </summary>
    /// <remarks>
    /// 夹具里每个 Provider 的嵌入维度都是它
    /// </remarks>
    public int EmbeddingDimensions { get; }

    /// <summary>
    /// 默认嵌入 Provider 名
    /// </summary>
    /// <remarks>
    /// 只由聊天与嵌入分开设默认的夹具给出（不同于 <see cref="ProviderName"/>）；其余夹具不设
    /// </remarks>
    public string? EmbeddingProviderName
    {
        get => field;
        init => field = value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The embedding provider name must not be blank.", nameof(value));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}

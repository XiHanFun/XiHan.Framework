// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// 一个模型 Provider 的配置（<c>XiHan:AI:Providers:&lt;name&gt;</c>）。
/// </summary>
/// <remarks>
/// 种类、端点、凭据、模型与透传属性。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AIProviderOptions
{
    /// <summary>
    /// 默认超时。
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

    /// <summary>
    /// 创建。
    /// </summary>
    public AIProviderOptions(string name, string kind)
    {
        Name = AIIdentifiers.ValidateName(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Kind = kind.ToLowerInvariant();
    }

    /// <summary>
    /// 名字。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 种类（小写），由 <see cref="IChatClientFactory.Kind"/> / <see cref="IEmbeddingGeneratorFactory.Kind"/> 匹配。
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// 端点。
    /// </summary>
    /// <remarks>
    /// 空表示 Provider 的官方端点。
    /// </remarks>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// API 密钥。
    /// </summary>
    /// <remarks>
    /// 空时由 Provider 决定（本地兼容端点通常不需要）。
    /// </remarks>
    public string? ApiKey { get; init; }

    /// <summary>
    /// 聊天模型。
    /// </summary>
    public string? ChatModel { get; init; }

    /// <summary>
    /// 嵌入模型。
    /// </summary>
    public string? EmbeddingModel { get; init; }

    /// <summary>
    /// 嵌入维度。
    /// </summary>
    /// <remarks>
    /// 空表示用模型默认维度。
    /// </remarks>
    public int? EmbeddingDimensions
    {
        get => field;
        init => field = value is null or >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The dimensions must be at least 1.");
    }

    /// <summary>
    /// 单次调用超时。
    /// </summary>
    /// <remarks>
    /// 默认 100 秒。
    /// </remarks>
    public TimeSpan Timeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive.");
    } = DefaultTimeout;

    /// <summary>
    /// 透传给工厂的属性。
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// 一个模型 Provider 的配置（<c>XiHan:AI:Providers:&lt;name&gt;</c>）
/// </summary>
/// <remarks>
/// 种类、端点、凭据、模型与透传属性
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AIProviderOptions
{
    /// <summary>
    /// 默认超时
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

    /// <summary>
    /// 创建
    /// </summary>
    public AIProviderOptions(string name, string kind)
    {
        Name = AIIdentifiers.ValidateName(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Kind = kind.ToLowerInvariant();
    }

    /// <summary>
    /// 名字
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 种类（小写），由 <see cref="IChatClientFactory.Kind"/> / <see cref="IEmbeddingGeneratorFactory.Kind"/> 匹配
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// 端点
    /// </summary>
    /// <remarks>
    /// 空表示 Provider 的官方端点；有值时必须是绝对的 http 或 https 地址，否则抛出 <see cref="ArgumentException"/>，不会退回官方端点、把密钥发给它
    /// </remarks>
    public Uri? Endpoint
    {
        get => field;
        init => field = value is null || IsHttpUrl(value) ? value : throw new ArgumentException($"The endpoint '{value}' must be an absolute http or https URL.", nameof(value));
    }

    /// <summary>
    /// API 密钥
    /// </summary>
    /// <remarks>
    /// 空时由 Provider 决定（本地兼容端点通常不需要）
    /// </remarks>
    public string? ApiKey { get; init; }

    /// <summary>
    /// 聊天模型
    /// </summary>
    public string? ChatModel { get; init; }

    /// <summary>
    /// 嵌入模型
    /// </summary>
    public string? EmbeddingModel { get; init; }

    /// <summary>
    /// 嵌入维度
    /// </summary>
    /// <remarks>
    /// 空表示用模型默认维度
    /// </remarks>
    public int? EmbeddingDimensions
    {
        get => field;
        init => field = value is null or >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The dimensions must be at least 1.");
    }

    /// <summary>
    /// 单次调用超时
    /// </summary>
    /// <remarks>
    /// 默认 100 秒
    /// </remarks>
    public TimeSpan Timeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive.");
    } = DefaultTimeout;

    /// <summary>
    /// 透传给工厂的属性
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 是绝对的 http 或 https 地址
    /// </summary>
    internal static bool IsHttpUrl(Uri value) => value.IsAbsoluteUri && (value.Scheme == Uri.UriSchemeHttp || value.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，API 密钥不能明文出现（RFC-0014「选项里的凭据」）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("Name = ").Append(Name)
            .Append(", Kind = ").Append(Kind)
            .Append(", Endpoint = ").Append(Endpoint)
            .Append(", ApiKey = ").Append(Redaction.Redact(ApiKey))
            .Append(", ChatModel = ").Append(ChatModel)
            .Append(", EmbeddingModel = ").Append(EmbeddingModel)
            .Append(", EmbeddingDimensions = ").Append(EmbeddingDimensions)
            .Append(", Timeout = ").Append(Timeout)
            .Append(", Properties = ").Append(Properties);
        return true;
    }
}

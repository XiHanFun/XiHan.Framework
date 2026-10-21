// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 主题文法：小写字母或数字开头，之后是小写字母、数字、<c>.</c>、<c>_</c>、<c>-</c>，不超过 128 个字符。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class TopicName
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid(string? topic) => topic is not null && Pattern().IsMatch(topic);

    /// <summary>
    /// 校验并返回；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        return IsValid(topic)
            ? topic
            : throw new ArgumentException($"主题 '{topic}' 不合法：小写字母或数字开头，只含小写字母、数字、'.'、'_'、'-'，不超过 {MaxLength} 个字符。", nameof(topic));
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$")]
    private static partial Regex Pattern();
}

/// <summary>
/// 框架约定的 CloudEvents 扩展属性名。扩展属性名只含小写字母与数字、不超过 20 个字符。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class EnvelopeExtensions
{
    /// <summary>租户 ID，派发时进入 Baggage <c>xihan.tenant</c>。</summary>
    public const string Tenant = "xihantenant";

    /// <summary>关联 ID，派发时进入 Baggage <c>xihan.correlation</c>。</summary>
    public const string Correlation = "xihancorrelation";

    /// <summary>W3C <c>traceparent</c>（CloudEvents 分布式追踪扩展）。</summary>
    public const string TraceParent = "traceparent";

    /// <summary>
    /// 扩展属性名是否合法。
    /// </summary>
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    [GeneratedRegex("^[a-z0-9]{1,20}$")]
    private static partial Regex NamePattern();
}

/// <summary>
/// CloudEvents 1.0 信封。数据是字节；<see cref="DataContentType"/> 说明其格式，JSON 数据在结构化编码里内联，其他数据按 Base64 编码。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessageEnvelope
{
    private static readonly IReadOnlyDictionary<string, string> NoExtensions = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// CloudEvents 规范版本。
    /// </summary>
    public const string SpecVersion = "1.0";

    /// <summary>
    /// 事件 ID；与 <see cref="Source"/> 一起唯一。
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("事件 ID 不能为空。", nameof(value));
    }

    /// <summary>
    /// 来源（URI 引用，如 <c>/orders</c>）。
    /// </summary>
    public required string Source
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("来源不能为空。", nameof(value));
    }

    /// <summary>
    /// 事件类型（如 <c>orders.placed</c>）。
    /// </summary>
    public required string Type
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("事件类型不能为空。", nameof(value));
    }

    /// <summary>
    /// 发生时间。
    /// </summary>
    public DateTimeOffset? Time { get; init; }

    /// <summary>
    /// 主题标识（事件所涉对象）。
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// 数据内容类型；默认 <c>application/json</c>。
    /// </summary>
    public string DataContentType
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("数据内容类型不能为空。", nameof(value));
    } = "application/json";

    /// <summary>
    /// 数据。
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>
    /// 扩展属性；名称须满足 <see cref="EnvelopeExtensions.IsValidName"/>。
    /// </summary>
    public IReadOnlyDictionary<string, string> Extensions
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var name in value.Keys)
            {
                if (!EnvelopeExtensions.IsValidName(name))
                {
                    throw new ArgumentException($"扩展属性名 '{name}' 不合法：只含小写字母与数字、不超过 20 个字符。", nameof(value));
                }
            }

            field = value;
        }
    } = NoExtensions;

    /// <summary>
    /// 数据是否为 JSON（<c>application/json</c> 或 <c>+json</c> 结尾）。
    /// </summary>
    public bool IsJsonData
        => DataContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            || DataContentType.Split(';')[0].TrimEnd().EndsWith("+json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 派生带一个扩展属性的信封。
    /// </summary>
    public MessageEnvelope WithExtension(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var extensions = new Dictionary<string, string>(Extensions, StringComparer.Ordinal) { [name] = value };
        return this with { Extensions = extensions };
    }

    /// <summary>
    /// 读扩展属性。
    /// </summary>
    public Maybe<string> Extension(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Extensions.TryGetValue(name, out var value) ? Maybe.Some(value) : default;
    }
}

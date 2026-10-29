// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Envelopes;

/// <summary>
/// CloudEvents 1.0 信封。
/// </summary>
/// <remarks>
/// 数据是字节；<see cref="DataContentType"/> 说明其格式，JSON 数据在结构化编码里内联，其他数据按 Base64 编码。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MessageEnvelope
{
    /// <summary>
    /// 共享的空扩展表。
    /// </summary>
    /// <remarks>
    /// 没有扩展的信封都指向它。
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> _noExtensions = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// CloudEvents 规范版本。
    /// </summary>
    public const string SpecVersion = "1.0";

    /// <summary>
    /// 事件 ID。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Source"/> 一起唯一。
    /// </remarks>
    public required string Id
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The event ID must not be empty.", nameof(value));
    }

    /// <summary>
    /// 来源（URI 引用，如 <c>/orders</c>）。
    /// </summary>
    public required string Source
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The source must not be empty.", nameof(value));
    }

    /// <summary>
    /// 事件类型（如 <c>orders.placed</c>）。
    /// </summary>
    public required string Type
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The event type must not be empty.", nameof(value));
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
    /// 数据内容类型。
    /// </summary>
    /// <remarks>
    /// 默认 <c>application/json</c>。
    /// </remarks>
    public string DataContentType
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The data content type must not be empty.", nameof(value));
    } = "application/json";

    /// <summary>
    /// 数据。
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>
    /// 扩展属性。
    /// </summary>
    /// <remarks>
    /// 名称须满足 <see cref="EnvelopeExtensions.IsValidName"/>。
    /// </remarks>
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
                    throw new ArgumentException($"Extension attribute name '{name}' is invalid: it must contain only lowercase letters and digits and be at most 20 characters long.", nameof(value));
                }
            }

            field = value;
        }
    } = _noExtensions;

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

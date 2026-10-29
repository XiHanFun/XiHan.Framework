// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 一次调用在管道内的上下文。
/// </summary>
/// <remarks>
/// 不绑定 HTTP；跨进程传播的字段与载体由 RFC-0003 定义。
/// </remarks>
[ApiLevelAttribute(Stability.Stable, "5.0")]
public sealed partial class InvocationContext
{
    /// <summary>
    /// 调用作用域内的对象袋。
    /// </summary>
    /// <remarks>
    /// 派生上下文与源上下文共享同一个实例。
    /// </remarks>
    private readonly Dictionary<object, object?> _items;

    /// <summary>
    /// 创建上下文。
    /// </summary>
    public InvocationContext()
    {
        _items = [];
    }

    /// <summary>
    /// 从已有上下文派生。
    /// </summary>
    /// <remarks>
    /// 复制截止时间、Trace 与服务提供器，替换 Baggage 与取消令牌，并共享 <see cref="Items"/>。
    /// </remarks>
    /// <param name="source">源上下文。</param>
    /// <param name="baggage">派生上下文的 Baggage，调用方已校验。</param>
    /// <param name="cancellationToken">派生上下文的取消令牌。</param>
    [SetsRequiredMembers]
    private InvocationContext(InvocationContext source, IReadOnlyDictionary<string, string> baggage, CancellationToken cancellationToken)
    {
        // 直接引用源字典而非复制：中间件在派生上下文上写入的 Items 必须对上游可见（例如调用结果槽）。
        _items = source._items;
        CancellationToken = cancellationToken;
        Deadline = source.Deadline;
        TraceId = source.TraceId;
        SpanId = source.SpanId;
        Baggage = baggage;
        Services = source.Services;
    }

    /// <summary>
    /// 取消令牌。
    /// </summary>
    /// <remarks>
    /// 到达 <see cref="Deadline"/> 时必须触发。
    /// </remarks>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// 绝对截止时间（UTC）。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示无截止。下游调用的截止时间不得晚于上游。
    /// </remarks>
    public DateTimeOffset? Deadline { get; init; }

    /// <summary>
    /// W3C Trace Context 的 trace-id：32 位小写十六进制。
    /// </summary>
    public required string TraceId
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = TraceIdPattern().IsMatch(value)
                ? value
                : throw new ArgumentException("TraceId must be 32 lowercase hexadecimal characters.", nameof(value));
        }
    }

    /// <summary>
    /// W3C Trace Context 的 parent-id：16 位小写十六进制。
    /// </summary>
    public required string SpanId
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = SpanIdPattern().IsMatch(value)
                ? value
                : throw new ArgumentException("SpanId must be 16 lowercase hexadecimal characters.", nameof(value));
        }
    }

    /// <summary>
    /// 随调用传播的键值。
    /// </summary>
    /// <remarks>
    /// 键必须符合 <see cref="BaggageKeys.IsWellFormed"/>。
    /// </remarks>
    public required IReadOnlyDictionary<string, string> Baggage
    {
        get => field;
        init => field = ValidateBaggage(value);
    }

    /// <summary>
    /// 调用作用域内的对象袋。
    /// </summary>
    /// <remarks>
    /// 不跨进程传播。由 <see cref="WithBaggage"/> 派生的上下文共享同一个袋。
    /// </remarks>
    public IDictionary<object, object?> Items => _items;

    /// <summary>
    /// 调用作用域的服务提供器。
    /// </summary>
    /// <remarks>
    /// 不跨进程传播。
    /// </remarks>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// 读取 Baggage。
    /// </summary>
    public bool TryGetBaggage(string key, [MaybeNullWhen(false)] out string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Baggage.TryGetValue(key, out value);
    }

    /// <summary>
    /// 派生一个新增或覆盖了一项 Baggage 的上下文。
    /// </summary>
    /// <remarks>
    /// 其余字段与 <see cref="Items"/> 共享。
    /// </remarks>
    public InvocationContext WithBaggage(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!BaggageKeys.IsWellFormed(key))
        {
            throw new ArgumentException($"Baggage key '{key}' does not match the 'xihan.<name>' format.", nameof(key));
        }

        // Baggage 不可变：复制一份再写入，源上下文看不到新键。
        var copy = new Dictionary<string, string>(Baggage, StringComparer.Ordinal)
        {
            [key] = value,
        };
        return new InvocationContext(this, copy, CancellationToken);
    }

    /// <summary>
    /// 派生一个使用另一个取消令牌的上下文（例如把截止时间落为取消）。
    /// </summary>
    /// <remarks>
    /// 其余字段与 <see cref="Items"/> 共享。
    /// </remarks>
    public InvocationContext WithCancellation(CancellationToken cancellationToken) => new(this, Baggage, cancellationToken);

    /// <summary>
    /// 在 <paramref name="now"/> 时刻是否已超过截止时间。
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => Deadline is { } deadline && now >= deadline;

    /// <summary>
    /// 在 <paramref name="now"/> 时刻距截止时间的剩余时长。
    /// </summary>
    /// <remarks>
    /// 无截止时为 <see langword="null"/>，已超时为零。
    /// </remarks>
    public TimeSpan? RemainingTime(DateTimeOffset now)
    {
        if (Deadline is not { } deadline)
        {
            return null;
        }

        // 已超时统一收敛为零而不是负数，便于调用方直接作为超时时长使用。
        var remaining = deadline - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// 校验 Baggage 的每个键都符合 <c>xihan.&lt;name&gt;</c> 格式。
    /// </summary>
    /// <remarks>
    /// 通过时原样返回字典。
    /// </remarks>
    private static IReadOnlyDictionary<string, string> ValidateBaggage(IReadOnlyDictionary<string, string> baggage)
    {
        ArgumentNullException.ThrowIfNull(baggage);
        foreach (var key in baggage.Keys)
        {
            if (!BaggageKeys.IsWellFormed(key))
            {
                throw new ArgumentException($"Baggage key '{key}' does not match the 'xihan.<name>' format.", nameof(baggage));
            }
        }

        return baggage;
    }

    /// <summary>
    /// trace-id 格式的正则：32 位小写十六进制。
    /// </summary>
    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TraceIdPattern();

    /// <summary>
    /// parent-id 格式的正则：16 位小写十六进制。
    /// </summary>
    [GeneratedRegex("^[0-9a-f]{16}$")]
    private static partial Regex SpanIdPattern();
}

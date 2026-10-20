// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 一次调用在管道内的上下文。不绑定 HTTP；跨进程传播的字段与载体由 RFC-0003 定义。
/// </summary>
[ApiLevelAttribute(Stability.Stable, "5.0")]
public sealed partial class InvocationContext
{
    private readonly Dictionary<object, object?> _items;

    /// <summary>
    /// 创建上下文。
    /// </summary>
    public InvocationContext()
    {
        _items = [];
    }

    [SetsRequiredMembers]
    private InvocationContext(InvocationContext source, IReadOnlyDictionary<string, string> baggage, CancellationToken cancellationToken)
    {
        _items = source._items;
        CancellationToken = cancellationToken;
        Deadline = source.Deadline;
        TraceId = source.TraceId;
        SpanId = source.SpanId;
        Baggage = baggage;
        Services = source.Services;
    }

    /// <summary>
    /// 取消令牌。到达 <see cref="Deadline"/> 时必须触发。
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// 绝对截止时间（UTC）；<see langword="null"/> 表示无截止。下游调用的截止时间不得晚于上游。
    /// </summary>
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
                : throw new ArgumentException("TraceId 必须是 32 位小写十六进制。", nameof(value));
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
                : throw new ArgumentException("SpanId 必须是 16 位小写十六进制。", nameof(value));
        }
    }

    /// <summary>
    /// 随调用传播的键值；键必须符合 <see cref="BaggageKeys.IsWellFormed"/>。
    /// </summary>
    public required IReadOnlyDictionary<string, string> Baggage
    {
        get => field;
        init => field = ValidateBaggage(value);
    }

    /// <summary>
    /// 调用作用域内的对象袋；不跨进程传播。由 <see cref="WithBaggage"/> 派生的上下文共享同一个袋。
    /// </summary>
    public IDictionary<object, object?> Items => _items;

    /// <summary>
    /// 调用作用域的服务提供器；不跨进程传播。
    /// </summary>
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
    /// 派生一个新增或覆盖了一项 Baggage 的上下文；其余字段与 <see cref="Items"/> 共享。
    /// </summary>
    public InvocationContext WithBaggage(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!BaggageKeys.IsWellFormed(key))
        {
            throw new ArgumentException($"Baggage 键 '{key}' 不符合 xihan.<name> 格式。", nameof(key));
        }

        var copy = new Dictionary<string, string>(Baggage, StringComparer.Ordinal)
        {
            [key] = value,
        };
        return new InvocationContext(this, copy, CancellationToken);
    }

    /// <summary>
    /// 派生一个使用另一个取消令牌的上下文（例如把截止时间落为取消）；其余字段与 <see cref="Items"/> 共享。
    /// </summary>
    public InvocationContext WithCancellation(CancellationToken cancellationToken) => new(this, Baggage, cancellationToken);

    /// <summary>
    /// 在 <paramref name="now"/> 时刻是否已超过截止时间。
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => Deadline is { } deadline && now >= deadline;

    /// <summary>
    /// 在 <paramref name="now"/> 时刻距截止时间的剩余时长；无截止时为 <see langword="null"/>，已超时为零。
    /// </summary>
    public TimeSpan? RemainingTime(DateTimeOffset now)
    {
        if (Deadline is not { } deadline)
        {
            return null;
        }

        var remaining = deadline - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static IReadOnlyDictionary<string, string> ValidateBaggage(IReadOnlyDictionary<string, string> baggage)
    {
        ArgumentNullException.ThrowIfNull(baggage);
        foreach (var key in baggage.Keys)
        {
            if (!BaggageKeys.IsWellFormed(key))
            {
                throw new ArgumentException($"Baggage 键 '{key}' 不符合 xihan.<name> 格式。", nameof(baggage));
            }
        }

        return baggage;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TraceIdPattern();

    [GeneratedRegex("^[0-9a-f]{16}$")]
    private static partial Regex SpanIdPattern();
}

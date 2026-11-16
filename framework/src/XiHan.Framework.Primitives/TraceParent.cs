// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives;

/// <summary>
/// W3C <c>traceparent</c> 的解析与写出
/// </summary>
/// <remarks>
/// 各载体（HTTP、消息、插件协议）共用同一份文法（RFC-0003 规则 7，ADR-0235）：只认版本 <c>00</c> 与小写十六进制，trace-id 或 parent-id 全零不合法；写出时版本是 <c>00</c>，标志只写采样位。<c>default</c> 不是合法值：读 <see cref="TraceId"/> 或 <see cref="SpanId"/> 抛出 <see cref="InvalidOperationException"/>，<see cref="ToString"/> 返回空串
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public readonly partial record struct TraceParent
{
    /// <summary>
    /// trace-id 的文本，<c>default</c> 时为 <see langword="null"/>
    /// </summary>
    private readonly string? _traceId;

    /// <summary>
    /// parent-id 的文本，<c>default</c> 时为 <see langword="null"/>
    /// </summary>
    private readonly string? _spanId;

    /// <summary>
    /// 创建一个 <c>traceparent</c>
    /// </summary>
    /// <remarks>
    /// <paramref name="traceId"/> 是 32 位、<paramref name="spanId"/> 是 16 位小写十六进制，都不能全零，否则抛出 <see cref="ArgumentException"/>
    /// </remarks>
    /// <param name="traceId">trace-id</param>
    /// <param name="spanId">parent-id（本端的 span-id）</param>
    /// <param name="sampled">是否采样</param>
    public TraceParent(string traceId, string spanId, bool sampled)
    {
        ArgumentNullException.ThrowIfNull(traceId);
        ArgumentNullException.ThrowIfNull(spanId);
        if (!IsIdentifier(traceId, 32))
        {
            throw new ArgumentException("TraceId must be 32 lowercase hexadecimal characters and not all zeros.", nameof(traceId));
        }

        if (!IsIdentifier(spanId, 16))
        {
            throw new ArgumentException("SpanId must be 16 lowercase hexadecimal characters and not all zeros.", nameof(spanId));
        }

        _traceId = traceId;
        _spanId = spanId;
        Sampled = sampled;
    }

    /// <summary>
    /// trace-id：32 位小写十六进制
    /// </summary>
    public string TraceId => _traceId ?? throw new InvalidOperationException("A default TraceParent has no trace-id.");

    /// <summary>
    /// parent-id：16 位小写十六进制
    /// </summary>
    public string SpanId => _spanId ?? throw new InvalidOperationException("A default TraceParent has no parent-id.");

    /// <summary>
    /// 是否采样：trace-flags 的最低位
    /// </summary>
    public bool Sampled { get; }

    /// <summary>
    /// 解析 <c>traceparent</c>
    /// </summary>
    /// <remarks>
    /// 不合法（其他版本、大写十六进制、多余的段、全零的标识）返回 <see langword="false"/>，调用方按没有给出处理
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? text, out TraceParent value)
    {
        value = default;
        if (text is null || Grammar().Match(text) is not { Success: true } match)
        {
            return false;
        }

        var traceId = match.Groups["trace"].Value;
        var spanId = match.Groups["span"].Value;
        if (IsAllZeros(traceId) || IsAllZeros(spanId))
        {
            return false;
        }

        // 只取采样位，其余位不进上下文
        var flags = byte.Parse(match.Groups["flags"].ValueSpan, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        value = new TraceParent(traceId, spanId, (flags & 0x01) == 0x01);
        return true;
    }

    /// <summary>
    /// 写成 <c>00-&lt;trace-id&gt;-&lt;parent-id&gt;-&lt;00|01&gt;</c>
    /// </summary>
    public override string ToString()
        => _traceId is null ? string.Empty : $"00-{_traceId}-{_spanId}-{(Sampled ? "01" : "00")}";

    /// <summary>
    /// 是否为给定长度的小写十六进制且不全为零
    /// </summary>
    private static bool IsIdentifier(string text, int length)
        => text.Length == length && text.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')) && !IsAllZeros(text);

    /// <summary>
    /// 是否全为字符 <c>0</c>
    /// </summary>
    private static bool IsAllZeros(string text) => text.All(static c => c == '0');

    /// <summary>
    /// 版本 00 的 <c>traceparent</c> 文法
    /// </summary>
    [GeneratedRegex("^00-(?<trace>[0-9a-f]{32})-(?<span>[0-9a-f]{16})-(?<flags>[0-9a-f]{2})\\z")]
    private static partial Regex Grammar();
}

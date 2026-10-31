// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Publishing;

/// <summary>
/// 发布时从当前调用补齐信封的扩展属性。
/// </summary>
/// <remarks>
/// RFC-0009「信封」：<c>traceparent</c> 取当前调用的追踪 ID、父跨度与采样标志（RFC-0003 规则 7）；租户先取安全上下文再取 Baggage；主体取已认证主体的 ID，匿名时取 Baggage；关联与区域取 Baggage。信封上已有的扩展保持不变，不在调用内时原样返回。
/// </remarks>
internal static class InvocationEnvelopes
{
    /// <summary>
    /// 补齐一个信封。
    /// </summary>
    public static MessageEnvelope Enrich(MessageEnvelope envelope, IInvocationAccessor? invocation)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Current(invocation) is { } context ? Merge(envelope, FromContext(context)) : envelope;
    }

    /// <summary>
    /// 补齐一批信封。
    /// </summary>
    /// <remarks>
    /// 从调用取值只做一次；没有要补的扩展时返回原列表。
    /// </remarks>
    public static IReadOnlyList<MessageEnvelope> Enrich(IReadOnlyList<MessageEnvelope> envelopes, IInvocationAccessor? invocation)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        if (envelopes.Count == 0 || Current(invocation) is not { } context)
        {
            return envelopes;
        }

        var extensions = FromContext(context);
        var enriched = new MessageEnvelope[envelopes.Count];
        for (var index = 0; index < envelopes.Count; index++)
        {
            ArgumentNullException.ThrowIfNull(envelopes[index]);
            enriched[index] = Merge(envelopes[index], extensions);
        }

        return enriched;
    }

    /// <summary>
    /// 当前调用的上下文。
    /// </summary>
    /// <remarks>
    /// 没有访问器或不在调用内为 <see langword="null"/>。
    /// </remarks>
    private static InvocationContext? Current(IInvocationAccessor? invocation)
        => invocation is not null && invocation.Current.TryGetValue(out var context) ? context : null;

    /// <summary>
    /// 从调用上下文算出要补的扩展。
    /// </summary>
    private static Dictionary<string, string> FromContext(InvocationContext context)
    {
        var extensions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EnvelopeExtensions.TraceParent] = TraceParentOf(context),
        };

        // 租户与主体先看安全上下文：HTTP + JWT 这条路上认证后的主体与解析出的租户不写进 Baggage；匿名或只带 Baggage 的调用再回退到 Baggage
        var security = context.GetSecurity();
        foreach (var (extension, baggageKey) in EnvelopeExtensions.BaggageMappings)
        {
            var value = extension switch
            {
                EnvelopeExtensions.Tenant when security.Tenant is { } tenant => tenant.Id,
                EnvelopeExtensions.Principal when security.Principal.IsAuthenticated => security.Principal.Id,
                _ => context.TryGetBaggage(baggageKey, out var carried) ? carried : null,
            };
            if (!string.IsNullOrEmpty(value))
            {
                extensions[extension] = value;
            }
        }

        return extensions;
    }

    /// <summary>
    /// 当前调用的 W3C <c>traceparent</c>。
    /// </summary>
    /// <remarks>
    /// 当前 Activity 与调用同一 trace 且有采样决定（Observe 阶段的 <c>xihan.invoke</c> 或它的子 Span）时以它为父级、带它的采样标志，消费端是它的子 Span；否则用调用的父跨度与标志，上游「不采样」原样传下去，没有上游（链路的根）按已采样处理。
    /// </remarks>
    private static string TraceParentOf(InvocationContext context)
    {
        // 有采样决定：经具名 ActivitySource 创建，或有父级；不经 ActivitySource 直接构造的根 Activity 的标志只是缺省值，不代表「不采样」
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } current
            && string.Equals(current.TraceId.ToHexString(), context.TraceId, StringComparison.Ordinal)
            && (current.Source.Name.Length > 0 || current.ParentSpanId != default))
        {
            return Format(context.TraceId, current.SpanId.ToHexString(), current.ActivityTraceFlags);
        }

        return Format(context.TraceId, context.SpanId, context.TraceFlags ?? ActivityTraceFlags.Recorded);
    }

    /// <summary>
    /// 按 W3C 版本 00 拼出 <c>traceparent</c>，标志只保留采样位。
    /// </summary>
    private static string Format(string traceId, string spanId, ActivityTraceFlags flags)
        => $"00-{traceId}-{spanId}-{((byte)(flags & ActivityTraceFlags.Recorded)).ToString("x2", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// 把调用里的扩展并进信封，信封已有的扩展优先。
    /// </summary>
    /// <remarks>
    /// 都已存在时返回原信封。
    /// </remarks>
    private static MessageEnvelope Merge(MessageEnvelope envelope, Dictionary<string, string> extensions)
    {
        if (extensions.Keys.All(envelope.Extensions.ContainsKey))
        {
            return envelope;
        }

        var merged = new Dictionary<string, string>(extensions, StringComparer.Ordinal);
        foreach (var (name, value) in envelope.Extensions)
        {
            merged[name] = value;
        }

        return envelope with { Extensions = merged };
    }
}

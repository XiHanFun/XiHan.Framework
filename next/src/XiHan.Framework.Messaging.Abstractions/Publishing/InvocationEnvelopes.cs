// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Publishing;

/// <summary>
/// 发布时从当前调用补齐信封的扩展属性。
/// </summary>
/// <remarks>
/// RFC-0009「信封」：<c>traceparent</c> 取当前调用的追踪 ID 与跨度 ID；租户先取安全上下文再取 Baggage；主体取已认证主体的 ID，匿名时取 Baggage；关联与区域取 Baggage。信封上已有的扩展保持不变，不在调用内时原样返回。
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
            [EnvelopeExtensions.TraceParent] = $"00-{context.TraceId}-{context.SpanId}-01",
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

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using Grpc.Core;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 调用上下文与协议 <c>InvocationMetadata</c> 之间的映射
/// </summary>
/// <remarks>
/// RFC-0003、<c>spec/plugin/invocation.md</c>
/// </remarks>
internal static class PluginMetadata
{
    /// <summary>
    /// 上下文 → 元数据：traceparent、绝对截止时间与已登记的 Baggage
    /// </summary>
    /// <remarks>
    /// traceparent 与消息信封同一取法（<see cref="InvocationContextExtensions.OutgoingTraceParent"/>）：以前标志写死为 <c>01</c>，上游「不采样」传不到插件（ADR-0235）
    /// </remarks>
    public static InvocationMetadata FromContext(InvocationContext context)
    {
        var metadata = new InvocationMetadata { TraceParent = context.OutgoingTraceParent().ToString() };
        if (context.Deadline is { } deadline)
        {
            metadata.Deadline = deadline.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        foreach (var (key, value) in context.Baggage)
        {
            // 未登记的键不出进程（RFC-0003 规则 3）
            if (BaggageKeys.IsRegistered(key))
            {
                metadata.Baggage[key] = value;
            }
        }

        // 租户写这次调用的租户，插件回调能带的租户以它为限（ADR-0195）：裁定过时不是调用方自报的那个
        if (TenantOf(context) is { } tenant)
        {
            metadata.Baggage[BaggageKeys.Tenant] = tenant;
        }
        else
        {
            metadata.Baggage.Remove(BaggageKeys.Tenant);
        }

        return metadata;
    }

    /// <summary>
    /// 宿主随这次调用交给插件的租户
    /// </summary>
    /// <remarks>
    /// <c>spec/plugin/invocation.md</c> §2（ADR-0195）：安全上下文有租户时取它；Tenant 阶段裁定为无租户时为 <see langword="null"/>，不回退到调用方自报的 Baggage；还没有裁定（宿主没有 Tenant 阶段，或调用还没走到它）时取调用携带的 Baggage
    /// </remarks>
    public static string? TenantOf(InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetSecurity().Tenant is { } tenant)
        {
            return tenant.Id;
        }

        return !context.IsTenantResolved() && context.TryGetBaggage(BaggageKeys.Tenant, out var carried) ? carried : null;
    }

    /// <summary>
    /// 调用插件时的 gRPC 调用选项：截止时间与取消令牌都来自上下文
    /// </summary>
    public static CallOptions CallOptionsFor(InvocationContext context)
        => new(deadline: context.Deadline?.UtcDateTime, cancellationToken: context.CancellationToken);

    /// <summary>
    /// 元数据 → 新的调用上下文（宿主回调时使用）
    /// </summary>
    /// <remarks>
    /// 不合法的 traceparent 视为缺失，未登记的 Baggage 键剥离，主体 Baggage 覆盖为插件自己的身份。给了 <paramref name="notAfter"/> 时截止时间取它与元数据里的截止时间中较早者（元数据没给时就是它），插件不能借回调把截止时间往后推
    /// </remarks>
    public static InvocationContext CreateContext(IServiceProvider scopedServices, InvocationMetadata? metadata, string principalId, CancellationToken cancellationToken, DateTimeOffset? notAfter = null)
    {
        string? traceId = null;
        string? spanId = null;
        ActivityTraceFlags? traceFlags = null;
        DateTimeOffset? deadline = null;
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is not null)
        {
            // 与其他载体同一文法，连同采样位一起取（RFC-0003 规则 7，ADR-0235）
            if (TraceParent.TryParse(metadata.TraceParent, out var traceparent))
            {
                traceId = traceparent.TraceId;
                spanId = traceparent.SpanId;
                traceFlags = traceparent.Sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
            }

            if (DateTimeOffset.TryParse(metadata.Deadline, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                deadline = parsed;
            }

            foreach (var (key, value) in metadata.Baggage)
            {
                if (BaggageKeys.IsRegistered(key))
                {
                    baggage[key] = value;
                }
            }
        }

        if (notAfter is { } cap && (deadline is null || cap < deadline))
        {
            deadline = cap;
        }

        // 回调不能冒充别的主体：xihan.principal 一律改写成插件的清单 ID（invocation.md §5）
        baggage[BaggageKeys.Principal] = principalId;
        return App.CreateContext(scopedServices, new InvocationContextOptions { TraceId = traceId, SpanId = spanId, TraceFlags = traceFlags, Deadline = deadline, Baggage = baggage }, cancellationToken);
    }
}

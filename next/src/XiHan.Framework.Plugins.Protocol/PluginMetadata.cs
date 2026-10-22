// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.RegularExpressions;
using Grpc.Core;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 调用上下文与协议 <c>InvocationMetadata</c> 之间的映射（RFC-0003、<c>spec/plugin/invocation.md</c>）。
/// </summary>
internal static partial class PluginMetadata
{
    /// <summary>
    /// 上下文 → 元数据：traceparent、绝对截止时间与已登记的 Baggage。
    /// </summary>
    public static InvocationMetadata FromContext(InvocationContext context)
    {
        var metadata = new InvocationMetadata { TraceParent = $"00-{context.TraceId}-{context.SpanId}-01" };
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

        return metadata;
    }

    /// <summary>
    /// 调用插件时的 gRPC 调用选项：截止时间与取消令牌都来自上下文。
    /// </summary>
    public static CallOptions CallOptionsFor(InvocationContext context)
        => new(deadline: context.Deadline?.UtcDateTime, cancellationToken: context.CancellationToken);

    /// <summary>
    /// 元数据 → 新的调用上下文（宿主回调时使用）：不合法的 traceparent 视为缺失，未登记的 Baggage 键剥离，主体 Baggage 覆盖为插件自己的身份。
    /// </summary>
    public static InvocationContext CreateContext(IServiceProvider scopedServices, InvocationMetadata? metadata, string principalId, CancellationToken cancellationToken)
    {
        string? traceId = null;
        string? spanId = null;
        DateTimeOffset? deadline = null;
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is not null)
        {
            if (TraceParent().Match(metadata.TraceParent) is { Success: true } match)
            {
                traceId = match.Groups["trace"].Value;
                spanId = match.Groups["span"].Value;
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

        // 回调不能冒充别的主体：xihan.principal 一律改写成插件的清单 ID（invocation.md §5）
        baggage[BaggageKeys.Principal] = principalId;
        return App.CreateContext(scopedServices, cancellationToken, traceId, spanId, deadline, baggage);
    }

    /// <summary>
    /// W3C traceparent 文法。
    /// </summary>
    [GeneratedRegex("^00-(?<trace>[0-9a-f]{32})-(?<span>[0-9a-f]{16})-[0-9a-f]{2}$")]
    private static partial Regex TraceParent();
}

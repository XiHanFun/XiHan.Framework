// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// HTTP 请求与调用上下文之间的翻译（RFC-0003 的 HTTP 载体映射）。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class HttpInvocation
{
    /// <summary>
    /// 截止时间头：RFC 3339，UTC。
    /// </summary>
    public const string DeadlineHeader = "xihan-deadline";

    private const string ContextItemKey = "xihan.invocation-context";

    /// <summary>
    /// 从请求头构造调用上下文：<c>traceparent</c>（缺失时生成）、<c>baggage</c>（只保留 <c>xihan.</c> 键）、<c>xihan-deadline</c>、
    /// <c>Authorization</c>（原样放入凭据，由 Authenticate 阶段解释）。
    /// </summary>
    public static InvocationContext CreateContext(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var headers = httpContext.Request.Headers;

        string? traceId = null;
        string? spanId = null;
        var traceparent = TraceParent().Match(headers["traceparent"].ToString());
        if (traceparent.Success)
        {
            traceId = traceparent.Groups["trace"].Value;
            spanId = traceparent.Groups["span"].Value;
        }

        DateTimeOffset? deadline = null;
        if (DateTimeOffset.TryParse(headers[DeadlineHeader].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            deadline = parsed;
        }

        var context = App.CreateContext(httpContext.RequestServices, httpContext.RequestAborted, traceId, spanId, deadline, ParseBaggage(headers["baggage"].ToString()));
        if (AuthenticationCredential.TryParse(headers.Authorization.ToString(), out var credential))
        {
            context.SetCredential(credential);
        }

        httpContext.Items[ContextItemKey] = context;
        return context;
    }

    /// <summary>
    /// 取当前请求的调用上下文；请求未经过 <c>UseXiHan()</c> 时为无值。
    /// </summary>
    public static Maybe<InvocationContext> GetXiHanContext(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.Items.TryGetValue(ContextItemKey, out var value) && value is InvocationContext context ? Maybe.Some(context) : default;
    }

    /// <summary>
    /// 解析 W3C <c>baggage</c> 头，只保留格式合法的 <c>xihan.</c> 键；值按 URL 解码。
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseBaggage(string? header)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(header))
        {
            return result;
        }

        foreach (var member in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = member.Split(';', 2)[0];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = pair[..separator].Trim();
            if (!BaggageKeys.IsWellFormed(key))
            {
                continue;
            }

            result[key] = Uri.UnescapeDataString(pair[(separator + 1)..].Trim());
        }

        return result;
    }

    /// <summary>
    /// 把 Baggage 编码为 W3C <c>baggage</c> 头的值。
    /// </summary>
    public static string FormatBaggage(IReadOnlyDictionary<string, string> baggage)
    {
        ArgumentNullException.ThrowIfNull(baggage);
        return string.Join(",", baggage.Select(static pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
    }

    [GeneratedRegex("^00-(?<trace>[0-9a-f]{32})-(?<span>[0-9a-f]{16})-[0-9a-f]{2}$")]
    private static partial Regex TraceParent();
}

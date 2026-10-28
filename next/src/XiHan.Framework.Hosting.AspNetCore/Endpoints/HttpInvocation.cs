// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

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

    /// <summary>
    /// gRPC 的相对超时头：<c>{数值}{H|M|S|m|u|n}</c>；没有 <see cref="DeadlineHeader"/> 时按当前时刻换算为截止时间。
    /// </summary>
    public const string GrpcTimeoutHeader = "grpc-timeout";

    /// <summary>
    /// 调用上下文在 <see cref="HttpContext.Items"/> 里的键。
    /// </summary>
    private const string ContextItemKey = "xihan.invocation-context";

    /// <summary>
    /// 从请求头构造调用上下文：<c>traceparent</c>（缺失时生成）、<c>baggage</c>（只保留 <c>xihan.</c> 键）、<c>xihan-deadline</c>（或 gRPC 的 <c>grpc-timeout</c>）、
    /// <c>Accept-Language</c>（没有显式 <c>xihan.locale</c> Baggage 时协商进去）、<c>Authorization</c>（原样放入凭据，由 Authenticate 阶段解释）；
    /// 调用来源（<see cref="InvocationOrigin"/>）记 HTTP 方法与路径、远端地址、<c>User-Agent</c> 与 <c>Referer</c>。
    /// </summary>
    public static InvocationContext CreateContext(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var context = CreateContext(httpContext, httpContext.RequestServices);
        httpContext.Items[ContextItemKey] = context;
        return context;
    }

    /// <summary>
    /// 以同一请求的头部构造另一个调用上下文：服务来自给定的作用域，不记录到 <see cref="HttpContext.Items"/>。
    /// 供协议适配器在一个 HTTP 请求里发起独立的子调用（如 MCP 的 <c>tools/call</c>），每个子调用有自己的作用域、工作单元与管道。
    /// </summary>
    public static InvocationContext CreateContext(HttpContext httpContext, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(services);
        var headers = httpContext.Request.Headers;

        // traceparent 不合法时视为缺失，由内核生成新的 trace-id，不把坏值带进链路
        string? traceId = null;
        string? spanId = null;
        var traceparent = TraceParent().Match(headers["traceparent"].ToString());
        if (traceparent.Success)
        {
            traceId = traceparent.Groups["trace"].Value;
            spanId = traceparent.Groups["span"].Value;
        }

        // 截止时间按不变文化解析并统一为 UTC；解析失败视为没有截止时间
        DateTimeOffset? deadline = null;
        if (DateTimeOffset.TryParse(headers[DeadlineHeader].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            deadline = parsed;
        }
        else if (GrpcTimeout().Match(headers[GrpcTimeoutHeader].ToString()) is { Success: true } timeout)
        {
            // gRPC 只给相对超时：以宿主时钟换算成绝对截止时间，纳秒按 100 纳秒刻度截断
            var amount = long.Parse(timeout.Groups["value"].Value, CultureInfo.InvariantCulture);
            var duration = timeout.Groups["unit"].Value switch
            {
                "H" => TimeSpan.FromHours(amount),
                "M" => TimeSpan.FromMinutes(amount),
                "S" => TimeSpan.FromSeconds(amount),
                "m" => TimeSpan.FromMilliseconds(amount),
                "u" => TimeSpan.FromMicroseconds(amount),
                _ => TimeSpan.FromTicks(amount / 100),
            };
            deadline = (services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow() + duration;
        }

        var baggage = ParseBaggageCore(headers["baggage"].ToString());

        // Accept-Language 只在调用方没有显式给出 xihan.locale 时生效：按权重排序的候选经本地化选项协商成启用的区域；
        // 没有装配本地化时取第一个合法值并规范化，让下游服务仍能拿到调用方语言
        if (!baggage.ContainsKey(BaggageKeys.Locale))
        {
            var accepted = AcceptedLocales(httpContext);
            if (accepted.Count > 0)
            {
                var options = services.GetService<LocalizationOptions>();
                baggage[BaggageKeys.Locale] = options is not null ? options.Negotiate(accepted) : LocaleName.Normalize(accepted[0]);
            }
        }

        var context = App.CreateContext(services, httpContext.RequestAborted, traceId, spanId, deadline, baggage);

        // 凭据只做格式拆分（scheme 与值），不在这里验证；解释交给安全子系统的 Authenticate 阶段
        if (AuthenticationCredential.TryParse(headers.Authorization.ToString(), out var credential))
        {
            context.SetCredential(credential);
        }

        context.SetOrigin(OriginOf(httpContext));
        return context;
    }

    /// <summary>
    /// HTTP 请求的调用来源：方法与路径（含 PathBase，不含查询串）、远端地址、User-Agent 与 Referer。
    /// 远端地址取连接上的对端地址：宿主在 <c>UseXiHan()</c> 之前 <c>UseForwardedHeaders()</c> 时即为转发头改写后的客户端地址；IPv4 映射的 IPv6 还原为 IPv4。
    /// </summary>
    private static InvocationOrigin OriginOf(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var address = httpContext.Connection.RemoteIpAddress;
        if (address is { IsIPv4MappedToIPv6: true })
        {
            address = address.MapToIPv4();
        }

        var path = request.PathBase.Add(request.Path).Value;
        return new InvocationOrigin
        {
            Protocol = InvocationProtocols.Http,
            Target = $"{request.Method} {(string.IsNullOrEmpty(path) ? "/" : path)}",
            RemoteAddress = address?.ToString(),
            UserAgent = HeaderOrNull(request.Headers.UserAgent.ToString()),
            Referer = HeaderOrNull(request.Headers.Referer.ToString()),
        };
    }

    /// <summary>
    /// 空头值视为没有。
    /// </summary>
    private static string? HeaderOrNull(string value) => value.Length > 0 ? value : null;

    /// <summary>
    /// 取当前请求的调用上下文；请求未经过 <c>UseXiHan()</c> 时为无值。
    /// </summary>
    public static Maybe<InvocationContext> GetXiHanContext(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.Items.TryGetValue(ContextItemKey, out var value) && value is InvocationContext context ? Maybe.Some(context) : default;
    }

    /// <summary>
    /// 从 <c>Accept-Language</c> 头取候选区域：按权重降序（同权重保持原顺序），跳过 <c>*</c>、权重为 0 与格式不合法的项。
    /// </summary>
    public static IReadOnlyList<string> AcceptedLocales(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var accepted = httpContext.Request.GetTypedHeaders().AcceptLanguage;
        if (accepted.Count == 0)
        {
            return [];
        }

        return accepted
            .Select(static (value, index) => (Locale: value.Value.ToString(), Quality: value.Quality ?? 1.0, Index: index))
            .Where(static v => v.Quality > 0 && !string.Equals(v.Locale, "*", StringComparison.Ordinal) && LocaleName.IsValid(v.Locale))
            .OrderByDescending(static v => v.Quality)
            .ThenBy(static v => v.Index)
            .Select(static v => v.Locale)
            .ToList();
    }

    /// <summary>
    /// 解析 W3C <c>baggage</c> 头，只保留格式合法的 <c>xihan.</c> 键；值按 URL 解码。
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseBaggage(string? header) => ParseBaggageCore(header);

    /// <summary>
    /// <see cref="ParseBaggage"/> 的可变字典版本，供 <see cref="CreateContext(HttpContext, IServiceProvider)"/> 在构造上下文前补入协商出的区域。
    /// </summary>
    private static Dictionary<string, string> ParseBaggageCore(string? header)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(header))
        {
            return result;
        }

        foreach (var member in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // 成员形如 key=value;prop1;prop2，分号后的属性不进入 Baggage
            var pair = member.Split(';', 2)[0];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            // 只收 xihan. 前缀且格式合法的键，其他键不属于本框架，直接丢弃
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

    /// <summary>
    /// W3C <c>traceparent</c> 头（版本 00）的匹配：捕获 32 位 trace-id 与 16 位 span-id。
    /// </summary>
    [GeneratedRegex("^00-(?<trace>[0-9a-f]{32})-(?<span>[0-9a-f]{16})-[0-9a-f]{2}$")]
    private static partial Regex TraceParent();

    /// <summary>
    /// gRPC <c>grpc-timeout</c> 头的匹配：最多 8 位数值加单位。
    /// </summary>
    [GeneratedRegex("^(?<value>[0-9]{1,8})(?<unit>[HMSmun])$")]
    private static partial Regex GrpcTimeout();
}

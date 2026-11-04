// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// HTTP 请求与调用上下文之间的翻译（RFC-0003 的 HTTP 载体映射）
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class HttpInvocation
{
    /// <summary>
    /// 截止时间头：RFC 3339，UTC
    /// </summary>
    public const string DeadlineHeader = "xihan-deadline";

    /// <summary>
    /// gRPC 的相对超时头
    /// </summary>
    /// <remarks>
    /// <c>{数值}{H|M|S|m|u|n}</c>；没有 <see cref="DeadlineHeader"/> 时按当前时刻换算为截止时间
    /// </remarks>
    public const string GrpcTimeoutHeader = "grpc-timeout";

    /// <summary>
    /// 调用上下文在 <see cref="HttpContext.Items"/> 里的键
    /// </summary>
    private const string ContextItemKey = "xihan.invocation-context";

    /// <summary>
    /// 从请求头构造调用上下文
    /// </summary>
    /// <remarks>
    /// <c>traceparent</c>（缺失时生成）、<c>baggage</c>（只保留已登记的键，其中 <see cref="BaggageKeys.Tenant"/> 与 <see cref="BaggageKeys.Principal"/> 不收：它们不能由调用方自报）、<c>xihan-deadline</c>（或 gRPC 的 <c>grpc-timeout</c>）、<c>Accept-Language</c>（没有显式 <c>xihan.locale</c> Baggage 时协商进去）、<c>Authorization</c>（原样放入凭据，由 Authenticate 阶段解释；没有时，登记过的查询串令牌路径下取 <c>access_token</c>，见 <see cref="InvocationAuthenticationOptions.QueryTokenPaths"/>）；调用来源（<see cref="InvocationOrigin"/>）记 HTTP 方法与路径、远端地址、<c>User-Agent</c> 与 <c>Referer</c>，以及匹配到的端点的路由模板与名字
    /// </remarks>
    public static InvocationContext CreateContext(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var context = CreateContext(httpContext, httpContext.RequestServices);
        httpContext.Items[ContextItemKey] = context;
        return context;
    }

    /// <summary>
    /// 以同一请求的头部构造另一个调用上下文
    /// </summary>
    /// <remarks>
    /// 服务来自给定的作用域，不记录到 <see cref="HttpContext.Items"/>。供协议适配器在一个 HTTP 请求里发起独立的子调用（如 MCP 的 <c>tools/call</c>），每个子调用有自己的作用域、工作单元与管道；也供绕过管道的端点（如 SignalR Hub 以连接的 <c>HttpContext</c>）发起内核调用，凭据随之带上，调用经管道时照常重新认证
    /// </remarks>
    public static InvocationContext CreateContext(HttpContext httpContext, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(services);
        var headers = httpContext.Request.Headers;

        // traceparent 不合法时视为缺失，不把坏值带进链路：内核并入 ASP.NET Core 当前的请求 Activity，也没有时生成新的 trace-id；
        // 合法时连同 trace-flags 一起取，上游「不采样」原样保留（RFC-0003 规则 7）
        string? traceId = null;
        string? spanId = null;
        ActivityTraceFlags? traceFlags = null;
        var traceparent = TraceParent().Match(headers["traceparent"].ToString());
        if (traceparent.Success)
        {
            traceId = traceparent.Groups["trace"].Value;
            spanId = traceparent.Groups["span"].Value;
            traceFlags = (ActivityTraceFlags)byte.Parse(traceparent.Groups["flags"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
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

        // 这个头是调用方自己写的。租户解析器只核对租户存在且启用，不核对调用方属不属于它：留着 xihan.tenant，
        // 任何没有租户声明的令牌都能进入任意租户；xihan.principal 同理，审计会把它当作没认证调用的主体（ADR-0121）
        baggage.Remove(BaggageKeys.Tenant);
        baggage.Remove(BaggageKeys.Principal);

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

        var context = App.CreateContext(services, httpContext.RequestAborted, traceId, spanId, deadline, baggage, traceFlags);

        // 凭据只做格式拆分（scheme 与值），不在这里验证；解释交给安全子系统的 Authenticate 阶段
        if (CredentialOf(httpContext).TryGetValue(out var credential))
        {
            context.SetCredential(credential);
        }

        context.SetOrigin(OriginOf(httpContext));
        return context;
    }

    /// <summary>
    /// HTTP 请求的调用来源
    /// </summary>
    /// <remarks>
    /// 方法与路径（含 PathBase，不含查询串）、远端地址、User-Agent 与 Referer，以及路由匹配到的端点的路由模板与名字（<c>UseXiHan()</c> 在路由之后，端点已选定）。远端地址取连接上的对端地址：宿主在 <c>UseXiHan()</c> 之前 <c>UseForwardedHeaders()</c> 时即为转发头改写后的客户端地址；IPv4 映射的 IPv6 还原为 IPv4
    /// </remarks>
    private static InvocationOrigin OriginOf(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var endpoint = httpContext.GetEndpoint();
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
            Route = (endpoint as RouteEndpoint)?.RoutePattern.RawText,
            EndpointName = endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
        };
    }

    /// <summary>
    /// 请求出示的凭据
    /// </summary>
    /// <remarks>
    /// 先取 <c>Authorization</c> 头；没有时，请求路径（不含 PathBase）按路径段落在登记的查询串令牌路径前缀下，且查询串 <c>access_token</c> 只有一个非空值，才作为 <c>Bearer</c> 凭据。其他路径的查询串令牌一律忽略。认证方案 <c>XiHan</c> 与 <see cref="CreateContext(HttpContext, IServiceProvider)"/> 共用这一条规则
    /// </remarks>
    internal static Maybe<AuthenticationCredential> CredentialOf(HttpContext httpContext)
    {
        var request = httpContext.Request;
        if (AuthenticationCredential.TryParse(request.Headers.Authorization.ToString(), out var credential))
        {
            return Maybe.Some(credential);
        }

        // 没有登记过方案时选项为缺省值（没有路径），查询串令牌一律不认
        var options = httpContext.RequestServices?.GetService<IOptionsMonitor<InvocationAuthenticationOptions>>()?.Get(InvocationAuthenticationDefaults.AuthenticationScheme);
        if (options is null || !options.AcceptsQueryToken(request.Path))
        {
            return Maybe.None<AuthenticationCredential>();
        }

        var values = request.Query[InvocationAuthenticationDefaults.QueryTokenParameter];
        return values.Count == 1 && !string.IsNullOrWhiteSpace(values[0])
            ? Maybe.Some(new AuthenticationCredential(InvocationAuthenticationDefaults.QueryTokenScheme, values[0]!.Trim()))
            : Maybe.None<AuthenticationCredential>();
    }

    /// <summary>
    /// 空头值视为没有
    /// </summary>
    private static string? HeaderOrNull(string value) => value.Length > 0 ? value : null;

    /// <summary>
    /// 取当前请求的调用上下文
    /// </summary>
    /// <remarks>
    /// 请求未经过 <c>UseXiHan()</c> 时为无值
    /// </remarks>
    public static Maybe<InvocationContext> GetXiHanContext(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.Items.TryGetValue(ContextItemKey, out var value) && value is InvocationContext context ? Maybe.Some(context) : default;
    }

    /// <summary>
    /// 从 <c>Accept-Language</c> 头取候选区域
    /// </summary>
    /// <remarks>
    /// 按权重降序（同权重保持原顺序），跳过 <c>*</c>、权重为 0 与格式不合法的项
    /// </remarks>
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
    /// 解析 W3C <c>baggage</c> 头，只保留已登记的键
    /// </summary>
    /// <remarks>
    /// 值按 URL 解码；格式合法但未登记的 <c>xihan.</c> 键与其他键都丢弃（RFC-0003 规则 3）。这里只管格式，入站的取舍在 <see cref="CreateContext(HttpContext, IServiceProvider)"/>：调用方自报的租户与主体不进调用上下文
    /// </remarks>
    public static IReadOnlyDictionary<string, string> ParseBaggage(string? header) => ParseBaggageCore(header);

    /// <summary>
    /// <see cref="ParseBaggage"/> 的可变字典版本，供 <see cref="CreateContext(HttpContext, IServiceProvider)"/> 在构造上下文前补入协商出的区域
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

            // 只收已登记的键（与插件协议、消息扩展属性一致）：别的系统的键与未登记的 xihan. 键都不进上下文
            var key = pair[..separator].Trim();
            if (!BaggageKeys.IsRegistered(key))
            {
                continue;
            }

            result[key] = Uri.UnescapeDataString(pair[(separator + 1)..].Trim());
        }

        return result;
    }

    /// <summary>
    /// 把 Baggage 编码为 W3C <c>baggage</c> 头的值
    /// </summary>
    /// <remarks>
    /// 只写已登记的键，进程内用的其他 <c>xihan.</c> 键不出进程（RFC-0003 规则 3）
    /// </remarks>
    public static string FormatBaggage(IReadOnlyDictionary<string, string> baggage)
    {
        ArgumentNullException.ThrowIfNull(baggage);
        return string.Join(",", baggage.Where(static pair => BaggageKeys.IsRegistered(pair.Key)).Select(static pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
    }

    /// <summary>
    /// W3C <c>traceparent</c> 头（版本 00）的匹配
    /// </summary>
    /// <remarks>
    /// 捕获 32 位 trace-id、16 位 span-id 与 2 位 trace-flags
    /// </remarks>
    [GeneratedRegex("^00-(?<trace>[0-9a-f]{32})-(?<span>[0-9a-f]{16})-(?<flags>[0-9a-f]{2})$")]
    private static partial Regex TraceParent();

    /// <summary>
    /// gRPC <c>grpc-timeout</c> 头的匹配：最多 8 位数值加单位
    /// </summary>
    [GeneratedRegex("^(?<value>[0-9]{1,8})(?<unit>[HMSmun])$")]
    private static partial Regex GrpcTimeout();
}

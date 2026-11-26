// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
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
    /// 截止时间头
    /// </summary>
    /// <remarks>
    /// 值是绝对截止时间的文本，文法见 <see cref="DeadlineText"/>（RFC-0003 规则 8）：如 <c>2026-11-18T03:25:00Z</c>、<c>2026-11-18T11:25:00.5+08:00</c>，必须有 <c>T</c>、秒与偏移（<c>Z</c> 或 <c>±HH:MM</c>）。单值头，按整串匹配、不裁剪；读不出（不合文法、越界、空值、出现多次）当作没有这个头，请求照常处理（<c>spec/protocol/http.md</c>「上下文映射」）；值不为空而读不出时另记一条限频的 Warning，不带原值（<see cref="UnreadableDeadlineLog"/>，ADR-0279）。写这个头用 <see cref="DeadlineText.Format"/>
    /// </remarks>
    public const string DeadlineHeader = "xihan-deadline";

    /// <summary>
    /// gRPC 的相对超时头
    /// </summary>
    /// <remarks>
    /// <c>{数值}{H|M|S|m|u|n}</c>，数值 1 到 8 位；没有 <see cref="DeadlineHeader"/> 或它读不出时按当前时刻换算为截止时间，自己读不出、出现多次同样当作没有（<c>spec/protocol/grpc.md</c>）；没有 <see cref="DeadlineHeader"/> 而它不为空又读不出时，同样记那条限频的 Warning
    /// </remarks>
    public const string GrpcTimeoutHeader = "grpc-timeout";

    /// <summary>
    /// 调用上下文在 <see cref="HttpContext.Items"/> 里的键
    /// </summary>
    private const string ContextItemKey = "xihan.invocation-context";

    /// <summary>
    /// 「这个请求读不出的截止时间已经报过」在 <see cref="HttpContext.Items"/> 里的键
    /// </summary>
    private const string UnreadableDeadlineItemKey = "xihan.unreadable-deadline-reported";

    /// <summary>
    /// 从请求头构造调用上下文
    /// </summary>
    /// <remarks>
    /// <c>traceparent</c>（缺失时生成）、<c>baggage</c>（只保留已登记的键，其中 <see cref="BaggageKeys.Tenant"/> 与 <see cref="BaggageKeys.Principal"/> 不收：它们不能由调用方自报）、<c>xihan-deadline</c>（按 <see cref="DeadlineText"/> 的文法，读不出当作没有；没有时取 gRPC 的 <c>grpc-timeout</c>）、<c>Accept-Language</c>（没有显式 <c>xihan.locale</c> Baggage 时协商进去）、<c>Authorization</c>（原样放入凭据，由 Authenticate 阶段解释；没有时，登记过的查询串令牌路径下取 <c>access_token</c>，见 <see cref="InvocationAuthenticationOptions.QueryTokenPaths"/>）；调用来源（<see cref="InvocationOrigin"/>）记 HTTP 方法与路径、远端地址、<c>User-Agent</c> 与 <c>Referer</c>，以及匹配到的端点的路由模板与名字
    /// </remarks>
    public static InvocationContext CreateContext(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        // 端点为请求里的消息另起内层调用时，外层不带幂等键：键交给内层调用去重（ADR-0205）
        var handsOver = httpContext.GetEndpoint()?.Metadata.GetMetadata<InnerInvocationsMetadata>() is not null;
        var context = Create(httpContext, httpContext.RequestServices, keepIdempotencyKey: !handsOver);
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
        return Create(httpContext, services, keepIdempotencyKey: true);
    }

    /// <summary>
    /// 从请求头构造调用上下文
    /// </summary>
    /// <remarks>
    /// <paramref name="keepIdempotencyKey"/> 为假时不带幂等键：外层请求把它交给端点另起的内层调用
    /// </remarks>
    private static InvocationContext Create(HttpContext httpContext, IServiceProvider services, bool keepIdempotencyKey)
    {
        var headers = httpContext.Request.Headers;

        // traceparent 不合法时视为缺失，不把坏值带进链路：内核并入 ASP.NET Core 当前的请求 Activity，也没有时生成新的 trace-id；
        // 合法时连同采样位一起取，上游「不采样」原样保留（RFC-0003 规则 7）；文法由原语 TraceParent 统一判断，全零的标识同样视为缺失（ADR-0235）
        string? traceId = null;
        string? spanId = null;
        ActivityTraceFlags? traceFlags = null;
        if (TraceParent.TryParse(headers["traceparent"].ToString(), out var traceparent))
        {
            traceId = traceparent.TraceId;
            spanId = traceparent.SpanId;
            traceFlags = traceparent.Sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
        }

        // 截止时间按规则 8 的文法整串匹配，文法由原语 DeadlineText 统一判断，与插件协议同一条（RFC-0003 规则 8，ADR-0276、ADR-0278）：
        // 值不裁剪、不换别的日期格式再试；它是单值头，出现多次时各个值以逗号并成一个文本，逗号不在文法里，哪一个都不取；
        // 读不出（含空值）当作没有这个头、不报错，让 grpc-timeout 照常生效
        DateTimeOffset? deadline = null;
        var deadlineText = headers[DeadlineHeader].ToString();
        if (DeadlineText.TryParse(deadlineText, out var parsed))
        {
            deadline = parsed;
        }
        else
        {
            var timeoutText = headers[GrpcTimeoutHeader].ToString();
            if (GrpcTimeout().Match(timeoutText) is { Success: true } timeout)
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
                // 八位数的小时能到一万多年，加上当前时刻会溢出 DateTimeOffset：截到能表示的最远时刻，等同于调用期间不会到期（ADR-0203）
                var now = (services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();
                deadline = duration < DateTimeOffset.MaxValue - now ? now + duration : DateTimeOffset.MaxValue;
            }

            // 调用方写了截止时间却读不出：调用照常继续，只留一条限频的告警（ADR-0279）。值为空是调用方没有要截止时间，不报；
            // xihan-deadline 读不出就报它，哪怕 grpc-timeout 接着生效；它没有给时才轮到读不出的 grpc-timeout
            if (deadlineText.Length > 0)
            {
                ReportUnreadableDeadline(httpContext, services, DeadlineHeader, deadlineText.Length);
            }
            else if (timeoutText.Length > 0 && deadline is null)
            {
                ReportUnreadableDeadline(httpContext, services, GrpcTimeoutHeader, timeoutText.Length);
            }
        }

        var baggage = ParseBaggageCore(headers["baggage"].ToString());

        // 这个头是调用方自己写的。租户解析器只核对租户存在且启用，不核对调用方属不属于它：留着 xihan.tenant，
        // 任何没有租户声明的令牌都能进入任意租户；xihan.principal 同理，审计会把它当作没认证调用的主体（ADR-0121）
        baggage.Remove(BaggageKeys.Tenant);
        baggage.Remove(BaggageKeys.Principal);
        if (!keepIdempotencyKey)
        {
            baggage.Remove(BaggageKeys.IdempotencyKey);
        }

        // Accept-Language 只在调用方没有显式给出 xihan.locale 时生效：按权重排序的候选经本地化选项协商成启用的区域；
        // 没有装配本地化时取第一个合法值并规范化，让下游服务仍能拿到调用方语言
        // 显式给的区域不合文法就当作没给，让 Accept-Language 照常协商，不让一个坏值把调用挡在默认区域（ADR-0204）
        if (baggage.TryGetValue(BaggageKeys.Locale, out var explicitLocale) && !LocaleName.IsValid(explicitLocale))
        {
            baggage.Remove(BaggageKeys.Locale);
        }

        if (!baggage.ContainsKey(BaggageKeys.Locale))
        {
            var accepted = AcceptedLocales(httpContext);
            if (accepted.Count > 0)
            {
                var options = services.GetService<LocalizationOptions>();
                baggage[BaggageKeys.Locale] = options is not null ? options.Negotiate(accepted) : LocaleName.Normalize(accepted[0]);
            }
        }

        var context = App.CreateContext(services, new InvocationContextOptions { TraceId = traceId, SpanId = spanId, Deadline = deadline, Baggage = baggage, TraceFlags = traceFlags }, httpContext.RequestAborted);

        // 凭据只做格式拆分（scheme 与值），不在这里验证；解释交给安全子系统的 Authenticate 阶段
        if (CredentialOf(httpContext).TryGetValue(out var credential))
        {
            context.SetCredential(credential);
        }

        context.SetOrigin(OriginOf(httpContext));
        return context;
    }

    /// <summary>
    /// 报告请求头里读不出的截止时间
    /// </summary>
    /// <remarks>
    /// 交给内核的 <see cref="UnreadableDeadlineLog"/> 限频记一条 Warning：只带头名、原值的长度与端点上的操作 ID，不带原值（ADR-0279）。容器里没有它（不是经 <c>AddXiHan</c> 装配的服务提供器）时不记
    /// </remarks>
    /// <param name="httpContext">当前请求</param>
    /// <param name="services">这次调用的服务提供器</param>
    /// <param name="header">读不出的头名</param>
    /// <param name="length">头的原值的字符数</param>
    private static void ReportUnreadableDeadline(HttpContext httpContext, IServiceProvider services, string header, int length)
    {
        // 一个请求可能建几次上下文（认证方案 XiHan、管道、MCP 的每个 tools/call）：只报一次，被压掉的条数才对得上请求数
        if (!httpContext.Items.TryAdd(UnreadableDeadlineItemKey, header))
        {
            return;
        }

        services.GetService<UnreadableDeadlineLog>()?.ReportHeader(header, length, httpContext.GetEndpoint()?.Metadata.GetMetadata<OperationDescriptor>()?.Id);
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
    public static Maybe<InvocationContext> FindXiHanContext(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.Items.TryGetValue(ContextItemKey, out var value) && value is InvocationContext context ? Maybe.Some(context) : default;
    }

    /// <summary>
    /// 读请求体，最多 <paramref name="maxBytes"/> 字节
    /// </summary>
    /// <remarks>
    /// 超过时没有值，读到的内容不留。<c>Content-Length</c> 已经超过的不读；没有声明长度（分块、HTTP/2）的边读边计数，读到上限多一个字节即停。读之前把服务器对这次请求的请求体上限压到 <paramref name="maxBytes"/>（只调低、不调高）；服务器自己的上限更小、读到一半报 413 时同样按超过处理，协议适配器因此总能用自己的错误格式答复
    /// </remarks>
    /// <param name="httpContext">当前请求</param>
    /// <param name="maxBytes">上限，不为负</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async ValueTask<Maybe<byte[]>> ReadBodyAsync(HttpContext httpContext, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        var request = httpContext.Request;
        if (request.ContentLength > maxBytes)
        {
            return Maybe.None<byte[]>();
        }

        // 没有长度声明时也不能读超：让服务器在读到上限时就中止，自己再按读到的字节数判一次
        if (httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit && (limit.MaxRequestBodySize is null || limit.MaxRequestBodySize > maxBytes))
        {
            limit.MaxRequestBodySize = maxBytes;
        }

        using var memory = new MemoryStream();
        var chunk = new byte[16 * 1024];
        try
        {
            int read;
            while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                // 计数在写进内存之前：超过上限的那一段不留
                if (memory.Length + read > maxBytes)
                {
                    return Maybe.None<byte[]>();
                }

                memory.Write(chunk, 0, read);
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // 服务器的上限比 maxBytes 小：同样是超过
            return Maybe.None<byte[]>();
        }

        return memory.ToArray();
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
    /// 只写已登记的键，进程内用的其他 <c>xihan.</c> 键不出进程（RFC-0003 规则 3）。值按 UTF-8 百分号编码；有孤立的代理项时抛出 <see cref="ArgumentException"/>，不写成 <c>%EF%BF%BD</c>（<c>conventions/baggage-keys.md</c>，ADR-0321）：两个只差在孤立代理项上的幂等键到了下游就是同一个，下游的 Idempotency 阶段把后一次调用当成前一次的重试。调用上下文里只有幂等键可能带着这样的值，其余已登记的键进上下文时已经核对过
    /// </remarks>
    /// <exception cref="ArgumentException">已登记的键的值有孤立的代理项</exception>
    public static string FormatBaggage(IReadOnlyDictionary<string, string> baggage)
    {
        ArgumentNullException.ThrowIfNull(baggage);
        return string.Join(",", baggage.Where(static pair => BaggageKeys.IsRegistered(pair.Key)).Select(static pair => $"{pair.Key}={EscapeValue(pair.Key, pair.Value)}"));
    }

    /// <summary>
    /// 把一个 Baggage 值按 UTF-8 百分号编码
    /// </summary>
    /// <remarks>
    /// <see cref="Uri.EscapeDataString(string)"/> 把孤立的代理项换成 <c>%EF%BF%BD</c>：先判定，编不成即抛出，不让两个不同的值写出同一段文本
    /// </remarks>
    private static string EscapeValue(string key, string value)
        => UnicodeText.HasLoneSurrogate(value)
            ? throw new ArgumentException($"The value of Baggage key '{key}' has a lone surrogate and cannot be percent-encoded as UTF-8.", nameof(value))
            : Uri.EscapeDataString(value);

    /// <summary>
    /// gRPC <c>grpc-timeout</c> 头的匹配：最多 8 位数值加单位
    /// </summary>
    [GeneratedRegex("^(?<value>[0-9]{1,8})(?<unit>[HMSmun])\\z")]
    private static partial Regex GrpcTimeout();
}

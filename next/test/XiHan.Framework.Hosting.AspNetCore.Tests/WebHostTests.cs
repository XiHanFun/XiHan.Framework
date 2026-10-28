// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.Abstractions.Authorization;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// ASP.NET Core 宿主集成验证：请求头到调用上下文的翻译、Problem Details 回写、探针与应用状态。
/// </summary>
public sealed class WebHostTests
{
    /// <summary>
    /// 捕获最近一次请求的调用上下文，供测试在响应之后断言。
    /// </summary>
    private sealed class Captured
    {
        /// <summary>
        /// 最近一次经过管道的调用上下文；尚无请求时为 <c>null</c>。
        /// </summary>
        public InvocationContext? Context { get; set; }
    }

    /// <summary>
    /// 把调用上下文写入 <see cref="Captured"/> 后放行的中间件。
    /// </summary>
    private sealed class CaptureMiddleware(Captured captured) : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
        {
            captured.Context = context;
            return nextStep(context);
        }
    }

    /// <summary>
    /// 用 TestServer 启动一个装配了捕获中间件与若干测试端点的应用，返回应用、客户端与捕获器。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client, Captured Captured)> StartAsync(Action<AppBuilder>? extra = null, Action<WebApplication>? beforeXiHan = null)
    {
        var captured = new Captured();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(captured);
        builder.AddXiHan(xihan =>
        {
            // 捕获中间件放在 Tenant 阶段：此时 Ingress 已完成翻译，凭据与授权要求都已进入上下文
            xihan.UseFeature(new StubFeature(TestManifests.Create("xihan.test.web"))
            {
                OnConfigure = context => context.AddMiddleware<CaptureMiddleware>(PipelineStage.Tenant),
            });
            extra?.Invoke(xihan);
        });

        var app = builder.Build();
        beforeXiHan?.Invoke(app);
        app.UseXiHan();
        app.MapXiHanHealth();
        app.MapGet("/ok", () => "ok");
        app.MapGet("/missing", (HttpContext http) =>
        {
            http.GetXiHanContext().ValueOrThrow().SetOutcome(Error.Literal("ORDERS_NOTFOUND_001", "The order does not exist."));
            return Results.Empty;
        });
        app.MapGet("/boom", static string () => throw new InvalidOperationException("boom"));
        app.MapGet("/secure", () => "secure").RequireXiHanPermission("orders.read");
        await app.StartAsync();
        return (app, app.GetTestClient(), captured);
    }

    [Fact]
    public async Task EndpointMetadata_BecomesAuthorizationRequirement()
    {
        var (app, client, captured) = await StartAsync();
        await using var _ = app;

        // 没有装配安全子系统时要求只进入上下文，不被判定
        var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AuthorizationRequirement.Permission("orders.read"), captured.Context!.GetAuthorizationRequirement().ValueOrThrow());
    }

    [Fact]
    public async Task AuthorizationHeader_BecomesCredential_WithoutInterpretation()
    {
        var (app, client, captured) = await StartAsync();
        await using var _ = app;

        var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer abc.def.ghi");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(captured.Context);
        var credential = captured.Context.GetCredential().ValueOrThrow();
        Assert.Equal("Bearer", credential.Scheme);
        Assert.Equal("abc.def.ghi", credential.Value);
        Assert.False(captured.Context.GetSecurity().Principal.IsAuthenticated);

        var anonymous = await client.GetAsync("/ok");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.False(captured.Context!.GetCredential().HasValue);
    }

    [Fact]
    public async Task Request_FlowsThroughKernelPipelineWithTraceAndBaggage()
    {
        var (app, client, captured) = await StartAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
        request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        request.Headers.Add("baggage", "xihan.tenant=t-42,ignored=1,xihan.locale=zh-CN;prop=x");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
        Assert.NotNull(captured.Context);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", captured.Context.TraceId);
        Assert.Equal("00f067aa0ba902b7", captured.Context.SpanId);
        Assert.Equal("t-42", captured.Context.Baggage[BaggageKeys.Tenant]);
        Assert.Equal("zh-CN", captured.Context.Baggage[BaggageKeys.Locale]);
        Assert.False(captured.Context.Baggage.ContainsKey("ignored"));
    }

    [Fact]
    public async Task Origin_RecordsMethodPathAndClient_HonouringForwardedHeaders()
    {
        var (app, _, captured) = await StartAsync(beforeXiHan: static web => web.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor }));
        await using var _ = app;
        var server = app.GetTestServer();

        // 直连：方法与路径（含 PathBase，不含查询串）、连接对端地址（IPv4 映射的 IPv6 还原为 IPv4）、User-Agent 与 Referer
        await server.SendAsync(static http =>
        {
            http.Request.Method = HttpMethods.Get;
            http.Request.PathBase = "/app";
            http.Request.Path = "/ok";
            http.Request.QueryString = new QueryString("?token=secret");
            http.Request.Headers.UserAgent = "probe/1.0";
            http.Request.Headers.Referer = "https://admin.test/users";
            http.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.7");
        });
        Assert.Equal(
            new InvocationOrigin { Protocol = InvocationProtocols.Http, Target = "GET /app/ok", RemoteAddress = "203.0.113.7", UserAgent = "probe/1.0", Referer = "https://admin.test/users" },
            captured.Context!.GetOrigin().ValueOrThrow());

        // 经反向代理：宿主在 UseXiHan() 之前 UseForwardedHeaders()，远端地址是转发头里的客户端；没有的头为 null
        await server.SendAsync(static http =>
        {
            http.Request.Method = HttpMethods.Get;
            http.Request.Path = "/ok";
            http.Request.Headers["X-Forwarded-For"] = "198.51.100.9";
            http.Connection.RemoteIpAddress = IPAddress.Loopback;
        });
        Assert.Equal(
            new InvocationOrigin { Protocol = InvocationProtocols.Http, Target = "GET /ok", RemoteAddress = "198.51.100.9" },
            captured.Context!.GetOrigin().ValueOrThrow());
    }

    [Fact]
    public async Task FailureOutcome_IsWrittenAsProblemDetails()
    {
        var (app, client, _) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ORDERS_NOTFOUND_001", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("urn:xihan:error:ORDERS_NOTFOUND_001", problem.RootElement.GetProperty("type").GetString());
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(32, problem.RootElement.GetProperty("traceId").GetString()!.Length);
    }

    [Fact]
    public async Task UnhandledException_BecomesInternalServerErrorProblem()
    {
        var (app, client, _) = await StartAsync();
        await using var _ = app;

        var response = await client.GetAsync("/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("KERNEL_UNEXPECTED_001", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExpiredDeadlineHeader_ReturnsGatewayTimeout()
    {
        var (app, client, _) = await StartAsync();
        await using var _ = app;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
        request.Headers.Add(HttpInvocation.DeadlineHeader, DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("KERNEL_TIMEOUT_001", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExpiredGrpcTimeoutHeader_ReturnsGatewayTimeout()
    {
        var (app, client, _) = await StartAsync();
        await using var _ = app;

        // 1 纳秒的相对超时在构造上下文时就已过期；xihan-deadline 缺失时才看 grpc-timeout
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
        request.Headers.Add(HttpInvocation.GrpcTimeoutHeader, "1n");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("KERNEL_TIMEOUT_001", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task HealthEndpoints_ReflectApplicationState()
    {
        var (app, client, _) = await StartAsync();
        await using var _ = app;

        foreach (var probe in new[] { "/health/startup", "/health/ready", "/health/live" })
        {
            var response = await client.GetAsync(probe);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("pass", body.RootElement.GetProperty("status").GetString());
            Assert.Equal("Ready", body.RootElement.GetProperty("state").GetString());
            Assert.Equal("xihan.test.web", body.RootElement.GetProperty("features")[0].GetProperty("id").GetString());
        }

        // 只停内核应用、不停 TestServer：探针应报 503，普通端点应被 UseXiHan 拦下返回 503
        await app.Services.GetRequiredService<App>().StopAsync(CancellationToken.None);

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var blocked = await client.GetAsync("/ok");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
    }

    [Fact]
    public void Baggage_RoundTripsThroughHeaderFormat()
    {
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = "t 42", [BaggageKeys.Correlation] = "a=b" };

        var header = HttpInvocation.FormatBaggage(baggage);
        var parsed = HttpInvocation.ParseBaggage(header);

        Assert.Equal("t 42", parsed[BaggageKeys.Tenant]);
        Assert.Equal("a=b", parsed[BaggageKeys.Correlation]);
        Assert.Empty(HttpInvocation.ParseBaggage(null));
    }

    [Fact]
    public void ProblemResponse_MapsKindsToStatusCodes()
    {
        Assert.Equal(400, ProblemResponse.StatusCodeFor(Error.Literal("ORDERS_VALIDATION_001", "x")));
        Assert.Equal(401, ProblemResponse.StatusCodeFor(Error.Literal("SECURITY_DENIED_001", "x")));
        Assert.Equal(403, ProblemResponse.StatusCodeFor(Error.Literal("SECURITY_DENIED_002", "x")));
        // 凭据无效 / 过期 / 签名不符也是认证失败：401（RFC 6750 §3.1），不是 403
        Assert.Equal(401, ProblemResponse.StatusCodeFor(Error.Literal("SECURITY_DENIED_003", "x")));
        Assert.Equal(403, ProblemResponse.StatusCodeFor(Error.Literal("SECURITY_DENIED_004", "x")));
        Assert.Equal(403, ProblemResponse.StatusCodeFor(Error.Literal("ORDERS_DENIED_001", "x")));
        Assert.Equal(409, ProblemResponse.StatusCodeFor(Error.Literal("ORDERS_CONFLICT_001", "x")));
        Assert.Equal(503, ProblemResponse.StatusCodeFor(Error.Literal("ORDERS_UNAVAILABLE_001", "x")));
        Assert.Equal(504, ProblemResponse.StatusCodeFor(Error.Literal("KERNEL_TIMEOUT_001", "x")));
        Assert.Equal(500, ProblemResponse.StatusCodeFor(Error.Literal("KERNEL_HOSTING_004", "x")));
    }

    [Fact]
    public void AuthenticationChallenge_NamesTheRejectedScheme_OrEveryRegisteredScheme()
    {
        string[] schemes = ["Bearer", "ApiKey", "bearer"];
        var invalid = Error.Literal("SECURITY_DENIED_003", "x");
        var anonymous = Error.Literal("SECURITY_DENIED_001", "x");

        // 凭据无效：只质询被拒的方案；Bearer 带 invalid_token（方案名按处理器登记的写法），其他方案只写名字
        Assert.Equal(["Bearer error=\"invalid_token\""], AuthenticationChallenge.For(invalid, Maybe.Some(new AuthenticationCredential("bearer", "expired")), schemes));
        Assert.Equal(["ApiKey"], AuthenticationChallenge.For(invalid, Maybe.Some(new AuthenticationCredential("ApiKey", "nope")), schemes));

        // 没有凭据：逐个质询已装配的方案（去重、不带错误码）；没有方案就没有质询
        Assert.Equal(["Bearer", "ApiKey"], AuthenticationChallenge.For(anonymous, Maybe.None<AuthenticationCredential>(), schemes));
        Assert.Empty(AuthenticationChallenge.For(anonymous, Maybe.None<AuthenticationCredential>(), []));

        // 不是认证失败（无权限、业务错误）不质询
        Assert.Empty(AuthenticationChallenge.For(Error.Literal("SECURITY_DENIED_004", "x"), Maybe.Some(new AuthenticationCredential("Bearer", "t")), schemes));
        Assert.True(ProblemResponse.IsAuthenticationFailure(invalid));
        Assert.False(ProblemResponse.IsAuthenticationFailure(Error.Literal("SECURITY_DENIED_002", "x")));
    }
}

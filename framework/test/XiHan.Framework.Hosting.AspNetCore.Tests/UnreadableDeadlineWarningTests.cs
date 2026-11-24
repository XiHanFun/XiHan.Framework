// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 请求头里的截止时间读不出时的限频告警
/// </summary>
/// <remarks>
/// <c>spec/protocol/http.md</c>「上下文映射」，ADR-0279。读不出记一条 Warning、窗口内的不再记、窗口过后的一条带被压掉的条数、日志里没有头的原值、读得出与没有给的不记；调用的结果不变。前几组直接经 <see cref="HttpInvocation.CreateContext(HttpContext, IServiceProvider)"/> 这一个读取点，最后一组走真实的宿主
/// </remarks>
public sealed class UnreadableDeadlineWarningTests
{
    /// <summary>
    /// 一个读不出的值：以前通用的日期解析读得出，还带着一段想混进日志的文本
    /// </summary>
    private const string Unreadable = "2026-11-18 03:25:00Z forged-log-line";

    [Fact]
    public void UnreadableDeadlineHeader_LogsOneWarning_WithoutTheValue_AndTheCallGoesOn()
    {
        using var host = new Fixture();

        var context = host.CreateContext((HttpInvocation.DeadlineHeader, [Unreadable]));

        Assert.Null(context.Deadline);
        var entry = Assert.Single(host.Warnings);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal($"The value of request header 'xihan-deadline' ({Unreadable.Length} characters) could not be read as a deadline and is ignored; the invocation continues without it. Similar warnings suppressed since the previous one: 0.", entry.Message);
        Assert.DoesNotContain(Unreadable, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("forged-log-line", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithinTheWindow_TheNextUnreadableHeaderIsNotLogged()
    {
        using var host = new Fixture();
        host.CreateContext((HttpInvocation.DeadlineHeader, [Unreadable]));

        host.Clock.Advance(UnreadableDeadlineLog.Window - TimeSpan.FromSeconds(1));
        var context = host.CreateContext((HttpInvocation.DeadlineHeader, ["2026-11-18"]));

        Assert.Null(context.Deadline);
        Assert.Single(host.Warnings);
    }

    [Fact]
    public void AfterTheWindow_TheNextWarningCarriesTheSuppressedCount()
    {
        using var host = new Fixture();
        host.CreateContext((HttpInvocation.DeadlineHeader, [Unreadable]));
        host.CreateContext((HttpInvocation.DeadlineHeader, ["2026-11-18"]));
        host.CreateContext((HttpInvocation.DeadlineHeader, ["03:25:00"]));

        host.Clock.Advance(UnreadableDeadlineLog.Window);
        host.CreateContext((HttpInvocation.DeadlineHeader, ["tomorrow"]));

        var warnings = host.Warnings;
        Assert.Equal(2, warnings.Count);
        Assert.Equal("The value of request header 'xihan-deadline' (8 characters) could not be read as a deadline and is ignored; the invocation continues without it. Similar warnings suppressed since the previous one: 2.", warnings[1].Message);
        Assert.DoesNotContain("tomorrow", warnings[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadableAbsentOrEmptyDeadlineHeaders_AreNotLogged()
    {
        using var host = new Fixture();

        Assert.NotNull(host.CreateContext((HttpInvocation.DeadlineHeader, ["2026-11-18T03:25:00Z"])).Deadline);
        Assert.NotNull(host.CreateContext((HttpInvocation.DeadlineHeader, ["2026-11-18T11:25:00.5+08:00"])).Deadline);
        Assert.NotNull(host.CreateContext((HttpInvocation.GrpcTimeoutHeader, ["30S"])).Deadline);
        Assert.Null(host.CreateContext().Deadline);
        // 值为空是调用方没有要截止时间，不是写错了
        Assert.Null(host.CreateContext((HttpInvocation.DeadlineHeader, [string.Empty])).Deadline);
        Assert.Null(host.CreateContext((HttpInvocation.GrpcTimeoutHeader, [string.Empty])).Deadline);

        Assert.Empty(host.Warnings);
    }

    [Fact]
    public void ARepeatedDeadlineHeader_IsLoggedAsUnreadable()
    {
        using var host = new Fixture();

        // 单值头出现两次：两个值以逗号并成一个文本，长度是并起来的长度
        var context = host.CreateContext((HttpInvocation.DeadlineHeader, ["2026-11-18T03:25:00Z", "2026-11-18T03:25:00Z"]));

        Assert.Null(context.Deadline);
        Assert.Contains("'xihan-deadline' (41 characters)", Assert.Single(host.Warnings).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableGrpcTimeout_IsLogged_OnlyWhenItIsTheOneBeingRead()
    {
        using var host = new Fixture();

        // xihan-deadline 读得出时不看 grpc-timeout，它写成什么都不记
        Assert.NotNull(host.CreateContext((HttpInvocation.DeadlineHeader, ["2026-11-18T03:25:00Z"]), (HttpInvocation.GrpcTimeoutHeader, ["soon"])).Deadline);
        Assert.Empty(host.Warnings);

        Assert.Null(host.CreateContext((HttpInvocation.GrpcTimeoutHeader, ["123456789S"])).Deadline);
        Assert.Equal("The value of request header 'grpc-timeout' (10 characters) could not be read as a deadline and is ignored; the invocation continues without it. Similar warnings suppressed since the previous one: 0.", Assert.Single(host.Warnings).Message);
    }

    [Fact]
    public void AnUnreadableDeadlineHeader_IsLogged_EvenWhenGrpcTimeoutTakesOver()
    {
        using var host = new Fixture();

        var context = host.CreateContext((HttpInvocation.DeadlineHeader, [Unreadable]), (HttpInvocation.GrpcTimeoutHeader, ["30S"]));

        Assert.Equal(host.Clock.GetUtcNow().AddSeconds(30), context.Deadline);
        Assert.Contains("'xihan-deadline'", Assert.Single(host.Warnings).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARequestThatBuildsSeveralContexts_CountsOnce()
    {
        using var host = new Fixture();
        var http = host.Request((HttpInvocation.DeadlineHeader, [Unreadable]));

        // 认证方案 XiHan、管道与 MCP 的每个 tools/call 都会以同一个请求建上下文
        HttpInvocation.CreateContext(http, host.Services);
        HttpInvocation.CreateContext(http, host.Services);
        HttpInvocation.CreateContext(http);
        host.Clock.Advance(UnreadableDeadlineLog.Window);
        host.CreateContext((HttpInvocation.DeadlineHeader, [Unreadable]));

        var warnings = host.Warnings;
        Assert.Equal(2, warnings.Count);
        Assert.EndsWith("Similar warnings suppressed since the previous one: 0.", warnings[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutTheKernelService_NothingIsLogged_AndTheCallGoesOn()
    {
        // 不是经 AddXiHan 装配的服务提供器（向量回放、手工构造的请求）里没有告警服务：读法不变，只是不记
        using var services = new ServiceCollection().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Headers[HttpInvocation.DeadlineHeader] = Unreadable;

        Assert.Null(HttpInvocation.CreateContext(http, services).Deadline);
    }

    [Fact]
    public async Task TheHost_LogsTheWarning_WithTheOperation_AndAnswersAsBefore()
    {
        var logs = new LogCapture();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(logs);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.AddXiHan(static xihan => xihan.UseFeature<PingFeature>());
        await using var app = builder.Build();
        app.UseXiHan();
        var descriptor = app.Services.GetRequiredService<OperationRegistry>().Find(PingOperation.Id).ValueOrThrow();
        app.MapPost("/pings", async (HttpContext http, IOperationInvoker invoker) =>
        {
            using var buffer = new MemoryStream();
            await http.Request.Body.CopyToAsync(buffer);
            var response = await invoker.ExecuteAsync(http.FindXiHanContext().ValueOrThrow(), new OperationRequest(PingOperation.Id, buffer.ToArray()));
            return response.IsSuccess ? Results.Bytes(response.Payload.ToArray(), "application/json") : Results.Empty;
        }).WithMetadata(descriptor);
        app.MapGet("/ok", static () => "ok");
        await app.StartAsync();
        var client = app.GetTestClient();

        // 第一条落在操作端点上，带操作 ID；窗口内落在普通端点上的两条被压掉；合法的头不算
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/pings", Unreadable)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/ok", "2020-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/ok", "2020-01-01T00:00:00")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/ok", "9999-12-31T23:59:59Z")).StatusCode);
        clock.Advance(UnreadableDeadlineLog.Window);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/ok", "2020-01-01")).StatusCode);

        var warnings = logs.Entries.Where(static entry => entry.Category == typeof(UnreadableDeadlineLog).FullName).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, static entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.Equal($"The value of request header 'xihan-deadline' ({Unreadable.Length} characters) could not be read as a deadline and is ignored; the invocation of operation '{PingOperation.Id}' continues without it. Similar warnings suppressed since the previous one: 0.", warnings[0].Message);
        Assert.Equal("The value of request header 'xihan-deadline' (10 characters) could not be read as a deadline and is ignored; the invocation continues without it. Similar warnings suppressed since the previous one: 2.", warnings[1].Message);
        Assert.DoesNotContain(logs.Entries, static entry => entry.Message.Contains("forged-log-line", StringComparison.Ordinal));
    }

    /// <summary>
    /// 带截止时间头发一个请求
    /// </summary>
    /// <param name="client">测试宿主的客户端</param>
    /// <param name="method">方法</param>
    /// <param name="path">路径</param>
    /// <param name="deadline"><c>xihan-deadline</c> 的值，原样写入</param>
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string deadline)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(HttpInvocation.DeadlineHeader, deadline);
        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent("""{"name":"XiHan"}""", Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// 只装了告警服务的服务提供器：日志收进内存，时钟可推进
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        /// <summary>
        /// 收到的日志
        /// </summary>
        private readonly LogCapture _logs = new();

        /// <summary>
        /// 服务提供器
        /// </summary>
        private readonly ServiceProvider _services;

        /// <summary>
        /// 创建
        /// </summary>
        public Fixture()
        {
            _services = new ServiceCollection()
                .AddSingleton<ILoggerFactory>(_logs)
                .AddSingleton<TimeProvider>(Clock)
                .AddSingleton<UnreadableDeadlineLog>()
                .BuildServiceProvider();
        }

        /// <summary>
        /// 时钟
        /// </summary>
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero));

        /// <summary>
        /// 服务提供器
        /// </summary>
        public IServiceProvider Services => _services;

        /// <summary>
        /// 收到的告警，按写入顺序
        /// </summary>
        public IReadOnlyList<LogCaptureEntry> Warnings => _logs.Entries;

        /// <summary>
        /// 带给定请求头的一个新请求
        /// </summary>
        /// <param name="headers">请求头：名字与值的列表，一个头出现几次就有几项</param>
        public DefaultHttpContext Request(params (string Name, string[] Values)[] headers)
        {
            var http = new DefaultHttpContext { RequestServices = _services };
            foreach (var (name, values) in headers)
            {
                http.Request.Headers[name] = new StringValues(values);
            }

            return http;
        }

        /// <summary>
        /// 以带给定请求头的一个新请求建调用上下文
        /// </summary>
        /// <param name="headers">请求头：名字与值的列表</param>
        public XiHan.Framework.Primitives.InvocationContext CreateContext(params (string Name, string[] Values)[] headers)
            => HttpInvocation.CreateContext(Request(headers), _services);

        /// <inheritdoc />
        public void Dispose() => _services.Dispose();
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

public sealed class WebHostTests
{
    private sealed class Captured
    {
        public InvocationContext? Context { get; set; }
    }

    private sealed class CaptureMiddleware(Captured captured) : IPipelineMiddleware
    {
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
        {
            captured.Context = context;
            return nextStep(context);
        }
    }

    private static async Task<(WebApplication App, HttpClient Client, Captured Captured)> StartAsync(Action<AppBuilder>? extra = null)
    {
        var captured = new Captured();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(captured);
        builder.AddXiHan(xihan =>
        {
            xihan.UseFeature(new StubFeature(TestManifests.Create("xihan.test.web"))
            {
                OnConfigure = context => context.AddMiddleware<CaptureMiddleware>(PipelineStage.Tenant),
            });
            extra?.Invoke(xihan);
        });

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanHealth();
        app.MapGet("/ok", () => "ok");
        app.MapGet("/missing", (HttpContext http) =>
        {
            http.GetXiHanContext().ValueOrThrow().SetOutcome(Error.NotFound("ORDERS_NOTFOUND_001", "没有"));
            return Results.Empty;
        });
        app.MapGet("/boom", static string () => throw new InvalidOperationException("boom"));
        await app.StartAsync();
        return (app, app.GetTestClient(), captured);
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
        Assert.Equal(400, ProblemResponse.StatusCodeFor(Error.Validation("ORDERS_VALIDATION_001", "x")));
        Assert.Equal(401, ProblemResponse.StatusCodeFor(Error.Denied("SECURITY_DENIED_001", "x")));
        Assert.Equal(403, ProblemResponse.StatusCodeFor(Error.Denied("SECURITY_DENIED_002", "x")));
        Assert.Equal(409, ProblemResponse.StatusCodeFor(Error.Conflict("ORDERS_CONFLICT_001", "x")));
        Assert.Equal(503, ProblemResponse.StatusCodeFor(Error.Unavailable("ORDERS_UNAVAILABLE_001", "x")));
        Assert.Equal(504, ProblemResponse.StatusCodeFor(new Error("KERNEL_TIMEOUT_001", "x")));
        Assert.Equal(500, ProblemResponse.StatusCodeFor(new Error("KERNEL_HOSTING_004", "x")));
    }
}

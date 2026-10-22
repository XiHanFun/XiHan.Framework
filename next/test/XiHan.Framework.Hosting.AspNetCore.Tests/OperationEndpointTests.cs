// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 操作描述作为端点元数据进入上下文，端点内用 <see cref="IOperationInvoker.ExecuteAsync(XiHan.Framework.Primitives.InvocationContext, OperationRequest)"/> 执行；失败结果由宿主写成 Problem Details。
/// </summary>
public sealed class OperationEndpointTests
{
    /// <summary>
    /// 启动带问候操作端点的测试宿主。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<PingFeature>());

        var app = builder.Build();
        app.UseXiHan();
        var descriptor = app.Services.GetRequiredService<OperationRegistry>().Find(PingOperation.Id).ValueOrThrow();
        app.MapPost("/pings", async (HttpContext http, IOperationInvoker invoker) =>
        {
            var context = http.GetXiHanContext().ValueOrThrow();
            using var buffer = new MemoryStream();
            await http.Request.Body.CopyToAsync(buffer);

            // 端点已在 Handle 阶段里：直接执行，失败结果留在上下文由宿主回写
            var response = await invoker.ExecuteAsync(context, new OperationRequest(PingOperation.Id, buffer.ToArray()));
            return response.IsSuccess ? Results.Bytes(response.Payload.ToArray(), "application/json") : Results.Empty;
        }).WithMetadata(descriptor);
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task Endpoint_ExecutesOperation_WithDescriptorInContext()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await client.PostAsync("/pings", new StringContent("""{"name":"XiHan"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Hello, XiHan!", body.RootElement.GetProperty("greeting").GetString());
        Assert.Equal(PingOperation.Id, body.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public async Task Endpoint_WritesOperationFailuresAsProblemDetails()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var invalid = await client.PostAsync("/pings", new StringContent("not json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.Equal("KERNEL_VALIDATION_001", problem.RootElement.GetProperty("code").GetString());

        var rejected = await client.PostAsync("/pings", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var rejection = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("PINGS_VALIDATION_001", rejection.RootElement.GetProperty("code").GetString());
    }
}

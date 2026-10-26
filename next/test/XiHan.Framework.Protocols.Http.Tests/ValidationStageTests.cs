// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// HTTP 请求在进入管道前绑定为载荷，Validate 阶段按契约 Schema 拒绝：缺必填、类型不符与嵌套违规都是 400 的 <c>KERNEL_VALIDATION_003</c>，不执行操作；合法请求照常执行；请求体不是 JSON 对象在进管道前就被拒绝。
/// </summary>
public sealed class ValidationStageTests
{
    /// <summary>
    /// 启动只装配 HTTP 协议与订单特性的测试宿主。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    /// <summary>
    /// 读 Problem Details 的错误码与 <c>detail</c>。
    /// </summary>
    private static async Task<(string Code, string? Detail)> ProblemOf(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (problem.RootElement.GetProperty("code").GetString()!, problem.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
    }

    /// <summary>
    /// JSON 请求体。
    /// </summary>
    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Post_WithSchemaViolations_Returns400_BeforeTheOperationRuns()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        // orders.create 带 Schema：page 必填且来自查询串；嵌套 line.sku 必须是字符串
        var missing = await client.PostAsync("/orders", Json("""{"name":"a"}"""));
        var nested = await client.PostAsync("/orders?page=1", Json("""{"name":"a","line":{"sku":5}}"""));
        var created = await client.PostAsync("/orders?page=1", Json("""{"name":"a","line":{"sku":"s"}}"""));

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var (code, detail) = await ProblemOf(missing);
        Assert.Equal(KernelErrors.SchemaViolation, code);
        Assert.Contains("required property 'page' is missing", detail, StringComparison.Ordinal);
        Assert.Equal(KernelErrors.SchemaViolation, (await ProblemOf(nested)).Code);
        Assert.Contains("$.line.sku", (await ProblemOf(nested)).Detail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Post_WithNonObjectBody_IsRejectedBeforeThePipeline()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await client.PostAsync("/orders?page=1", Json("[1,2]"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(KernelErrors.InvalidOperationPayload, (await ProblemOf(response)).Code);
    }

    [Fact]
    public async Task Get_WithoutSchema_IsNotValidated()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        // orders.list 没有登记 Schema：任何查询串都直达操作
        var response = await client.GetAsync("/orders?page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(KernelErrors.InvalidOperationPayload, (await ProblemOf(response)).Code);
    }
}

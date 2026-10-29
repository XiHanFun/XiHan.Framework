// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// Problem Details 的扩展成员。
/// </summary>
/// <remarks>
/// VALIDATION 错误的字段级 <c>errors</c> 按调用方区域翻译（与 <c>detail</c> 同一通道，没有键的文案原样），所有错误的 <c>details</c> 只收标量；没有装配本地化时字段文案是违规的原因。
/// </remarks>
public sealed class ProblemExtensionsTests
{
    /// <summary>
    /// 业务目录：只有英文与简体中文。
    /// </summary>
    private static readonly MessageCatalog _orders = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { ["orders.order.invalid"] = "Order is invalid.", ["orders.order.taken"] = "Order '{id}' is taken." },
        ["zh-CN"] = new Dictionary<string, string> { ["orders.order.invalid"] = "订单无效。", ["orders.order.taken"] = "订单 '{id}' 已被占用。" },
    });

    /// <summary>
    /// 带字段级违规的校验错误。
    /// </summary>
    /// <remarks>
    /// 内核键的违规两条（同一字段两条、嵌套路径一条）、应用文案一条；细节另带标量与集合。
    /// </remarks>
    private static Error Invalid()
    {
        IReadOnlyList<SchemaViolation> violations =
        [
            new("$.name", "length must be >= 2", KernelMessages.ValidationMinLength, MessageArguments.Of(("limit", 2))),
            new("$.name", "Name is reserved."),
            new("$.lines[0].sku", "required property 'sku' is missing", KernelMessages.ValidationRequired, MessageArguments.Of(("name", "sku"))),
        ];
        return Error.Validation("ORDERS_VALIDATION_001", "orders.order.invalid", MessageArguments.Of(("id", "orders.create"), ("errors", violations), ("violations", new List<string> { "x" })));
    }

    /// <summary>
    /// 带各种细节的冲突错误。
    /// </summary>
    /// <remarks>
    /// 标量进 <c>details</c>，对象与集合不进，<c>errors</c> 在非 VALIDATION 错误上不输出。
    /// </remarks>
    private static Error Conflict()
    {
        IReadOnlyList<SchemaViolation> violations = [new("$.name", "x")];
        return Error.Conflict("ORDERS_CONFLICT_001", "orders.order.taken", MessageArguments.Of(
            ("id", "o-1"),
            ("count", 3),
            ("ratio", 0.5),
            ("enabled", true),
            ("when", new DateTimeOffset(2026, 10, 27, 8, 30, 0, TimeSpan.Zero)),
            ("key", Guid.Empty),
            ("payload", new Dictionary<string, string>()),
            ("tags", new[] { "a" }),
            ("errors", violations)));
    }

    /// <summary>
    /// 启动测试宿主：<c>/invalid</c> 写校验错误，<c>/conflict</c> 写冲突错误。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(bool localization)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMessageCatalog(_orders);
        builder.AddXiHan(xihan =>
        {
            xihan.UseFeature(new StubFeature(TestManifests.Create("xihan.test.problems")));
            if (localization)
            {
                xihan.UseFeature<InMemoryLocalizationFeature>();
            }
        });

        var app = builder.Build();
        app.UseXiHan();
        app.MapGet("/invalid", (HttpContext http) =>
        {
            http.GetXiHanContext().ValueOrThrow().SetOutcome(Invalid());
            return Results.Empty;
        });
        app.MapGet("/conflict", (HttpContext http) =>
        {
            http.GetXiHanContext().ValueOrThrow().SetOutcome(Conflict());
            return Results.Empty;
        });
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    /// <summary>
    /// 发送 GET 请求，可带 <c>Accept-Language</c>。
    /// </summary>
    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? acceptLanguage = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        return client.SendAsync(request);
    }

    /// <summary>
    /// 读响应体为 JSON 文档。
    /// </summary>
    private static async Task<JsonDocument> BodyOf(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FieldErrors_AreLocalizedPerCallerLocale_AndKeepAppText()
    {
        var (app, client) = await StartAsync(localization: true);
        await using var host = app;

        var chinese = await GetAsync(client, "/invalid", acceptLanguage: "zh-CN");
        Assert.Equal(HttpStatusCode.BadRequest, chinese.StatusCode);
        Assert.Equal(["zh-CN"], chinese.Content.Headers.ContentLanguage);
        using var body = await BodyOf(chinese);
        Assert.Equal("订单无效。", body.RootElement.GetProperty("detail").GetString());
        var errors = body.RootElement.GetProperty("errors");
        // 字段按首次出现排序；同一字段的多条按出现顺序；没有键的应用文案原样
        Assert.Equal(["name", "lines[0].sku"], errors.EnumerateObject().Select(static e => e.Name));
        Assert.Equal(["长度必须 >= 2", "Name is reserved."], errors.GetProperty("name").EnumerateArray().Select(static e => e.GetString()));
        Assert.Equal(["缺少必填属性 'sku'"], errors.GetProperty("lines[0].sku").EnumerateArray().Select(static e => e.GetString()));
        // details 只收标量：id 进，errors 与 violations 不进
        var details = body.RootElement.GetProperty("details");
        Assert.Equal("orders.create", details.GetProperty("id").GetString());
        Assert.Single(details.EnumerateObject());

        // 英文走默认区域
        using var english = await BodyOf(await GetAsync(client, "/invalid", acceptLanguage: "en-US"));
        Assert.Equal("length must be >= 2", english.RootElement.GetProperty("errors").GetProperty("name")[0].GetString());
    }

    [Fact]
    public async Task Details_CarryOnlyScalars_AndErrorsOnlyOnValidationKind()
    {
        var (app, client) = await StartAsync(localization: true);
        await using var host = app;

        var response = await GetAsync(client, "/conflict", acceptLanguage: "zh-CN");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = await BodyOf(response);
        Assert.Equal("订单 'o-1' 已被占用。", body.RootElement.GetProperty("detail").GetString());
        Assert.False(body.RootElement.TryGetProperty("errors", out _));
        var details = body.RootElement.GetProperty("details");
        Assert.Equal("o-1", details.GetProperty("id").GetString());
        Assert.Equal(3, details.GetProperty("count").GetInt32());
        Assert.Equal(0.5, details.GetProperty("ratio").GetDouble());
        Assert.True(details.GetProperty("enabled").GetBoolean());
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 8, 30, 0, TimeSpan.Zero), details.GetProperty("when").GetDateTimeOffset());
        Assert.Equal(Guid.Empty, details.GetProperty("key").GetGuid());
        Assert.False(details.TryGetProperty("payload", out _));
        Assert.False(details.TryGetProperty("tags", out _));
        Assert.False(details.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task WithoutLocalization_FieldErrorsUseTheReasons()
    {
        var (app, client) = await StartAsync(localization: false);
        await using var host = app;

        using var body = await BodyOf(await GetAsync(client, "/invalid", acceptLanguage: "zh-CN"));

        Assert.Equal(["length must be >= 2", "Name is reserved."], body.RootElement.GetProperty("errors").GetProperty("name").EnumerateArray().Select(static e => e.GetString()));
        Assert.Equal(["required property 'sku' is missing"], body.RootElement.GetProperty("errors").GetProperty("lines[0].sku").EnumerateArray().Select(static e => e.GetString()));
    }

    [Fact]
    public void FromError_GroupsViolationsByField_AndFiltersScalars()
    {
        var problem = ProblemResponse.FromError(Invalid(), "/orders", new string('a', 32));

        Assert.Equal(["name", "lines[0].sku"], problem.Errors!.Keys);
        Assert.Equal(["length must be >= 2", "Name is reserved."], problem.Errors["name"]);
        Assert.Equal(["id"], problem.Details!.Keys);
        Assert.Null(ProblemResponse.FromError(Conflict(), "/orders", new string('a', 32)).Errors);
        Assert.Null(ProblemResponse.FromError(Error.Literal("ORDERS_CONFLICT_002", "x"), "/orders", new string('a', 32)).Details);
    }
}

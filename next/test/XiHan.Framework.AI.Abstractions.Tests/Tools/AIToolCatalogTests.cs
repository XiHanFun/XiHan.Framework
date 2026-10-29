// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Tests.Fixtures;
using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.AI.Abstractions.Tests.Tools;

/// <summary>
/// 工具目录。
/// </summary>
/// <remarks>
/// 操作桥接的声明与名字、描述按区域、显式工具、绑定后经完整管道执行、名字冲突启动失败。
/// </remarks>
public sealed class AIToolCatalogTests
{
    [Fact]
    public async Task Catalog_ListsExposedOperations_WithSchemaAndLocalizedDescription()
    {
        await using var host = await StartAsync([("XiHan:AI:Tools:echoes.rename", "renamed_tool")]);
        var catalog = host.Services.GetRequiredService<IAIToolCatalog>();

        var all = catalog.All();
        Assert.Equal(["echoes_create", "manual_add", "renamed_tool"], all.Select(static tool => tool.Name));
        var create = Assert.IsAssignableFrom<AIFunctionDeclaration>(catalog.Find("echoes_create").ValueOrThrow());
        Assert.Equal("Create an echo of the given text.", create.Description);
        Assert.Equal("object", create.JsonSchema.GetProperty("type").GetString());
        Assert.Equal("text", create.JsonSchema.GetProperty("required")[0].GetString());
        Assert.Equal("echo", create.ReturnJsonSchema!.Value.GetProperty("properties").EnumerateObject().First().Name);
        Assert.IsNotType<AIFunction>(create, exactMatch: false);

        var renamed = Assert.IsAssignableFrom<AIFunctionDeclaration>(catalog.Find("renamed_tool").ValueOrThrow());
        Assert.Equal("", renamed.Description);
        Assert.Equal("object", renamed.JsonSchema.GetProperty("type").GetString());
        Assert.Null(renamed.ReturnJsonSchema);
        Assert.False(catalog.Find("echoes_secret").HasValue);
        Assert.False(catalog.Find("echoes_audit").HasValue);
        Assert.IsType<AIFunction>(catalog.Find("manual_add").ValueOrThrow(), exactMatch: false);

        await using var scope = host.Services.CreateAsyncScope();
        var chinese = App.CreateContext(scope.ServiceProvider, CancellationToken.None, baggage: new Dictionary<string, string> { [BaggageKeys.Locale] = "zh-CN" });
        var bound = catalog.Bind(chinese);
        Assert.Equal("把给定文本回显出来。", bound.Single(static tool => tool.Name == "echoes_create").Description);
        Assert.Same(catalog.Find("manual_add").ValueOrThrow().GetType(), bound.Single(static tool => tool.Name == "manual_add").GetType());
    }

    [Fact]
    public async Task BoundOperationTool_ExecutesThroughThePipeline_AndReportsErrorsAsJson()
    {
        await using var host = await StartAsync();
        var catalog = host.Services.GetRequiredService<IAIToolCatalog>();
        await using var scope = host.Services.CreateAsyncScope();
        var caller = App.CreateContext(scope.ServiceProvider, CancellationToken.None, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "t1", [BaggageKeys.Locale] = "en-US" });
        var tool = Assert.IsType<OperationAIFunction>(catalog.Bind(caller).Single(static tool => tool.Name == "echoes_create"));
        Assert.Equal("echoes.create", tool.OperationId);

        var echoed = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments { ["text"] = "hi", ["repeat"] = 2 }, CancellationToken.None));
        Assert.Equal("hihi", echoed.GetProperty("echo").GetString());
        // 新上下文沿用调用方的追踪 ID 与 Baggage
        Assert.Equal(caller.TraceId, echoed.GetProperty("traceId").GetString());
        Assert.Equal("t1", echoed.GetProperty("tenant").GetString());

        using var fromJson = JsonDocument.Parse("""{"text":"yo","repeat":3}""");
        var viaElements = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments { ["text"] = fromJson.RootElement.GetProperty("text"), ["repeat"] = fromJson.RootElement.GetProperty("repeat") }, CancellationToken.None));
        Assert.Equal("yoyoyo", viaElements.GetProperty("echo").GetString());

        var failed = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments { ["text"] = "fail" }, CancellationToken.None));
        Assert.Equal("ECHOES_VALIDATION_001", failed.GetProperty("error").GetString());
        Assert.False(string.IsNullOrEmpty(failed.GetProperty("message").GetString()));

        var crashed = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments { ["text"] = "throw" }, CancellationToken.None));
        Assert.EndsWith("UNEXPECTED_001", crashed.GetProperty("error").GetString(), StringComparison.Ordinal);

        var manual = Assert.IsType<AIFunction>(catalog.Bind(caller).Single(static tool => tool.Name == "manual_add"), exactMatch: false);
        Assert.Equal(5, Assert.IsType<JsonElement>(await manual.InvokeAsync(new AIFunctionArguments { ["a"] = 2, ["b"] = 3 }, CancellationToken.None)).GetInt32());
    }

    [Fact]
    public async Task DuplicateOrInvalidToolNames_FailAtStartup()
    {
        var duplicate = AIFailure(await Assert.ThrowsAsync<FrameworkException>(() => StartAsync([("XiHan:AI:Tools:echoes.rename", "echoes_create")])));
        Assert.Equal(AIErrors.RequestInvalid, duplicate.Code);
        Assert.Contains("echoes_create", duplicate.Message, StringComparison.Ordinal);

        var invalid = AIFailure(await Assert.ThrowsAsync<FrameworkException>(() => StartAsync([("XiHan:AI:Tools:echoes.rename", "bad name!")])));
        Assert.Equal(AIErrors.RequestInvalid, invalid.Code);
    }

    [Fact]
    public void NameConvention_DerivesModelSafeNames()
    {
        Assert.Equal("orders_create", AIToolNameConvention.Derive(new OperationContract("orders.create")));
        Assert.Equal("order_items_bulk_import", AIToolNameConvention.Derive(new OperationContract("order-items.bulk-import")));
        Assert.Equal("orders.create.description", AIToolNameConvention.DescriptionKeyOf("orders.create"));
        Assert.Equal("order_items.bulk_import.description", AIToolNameConvention.DescriptionKeyOf("order-items.bulk-import"));
        // 资源段带连字符的操作派生的说明键必须是合法的消息键，否则目录收不下、描述也取不到
        Assert.True(MessageTemplate.IsValidKey(AIToolNameConvention.DescriptionKeyOf("order-items.bulk-import")));

        // 超过 64 个字符：前 55 个字符 + "_" + 全名哈希前 8 位，合法、稳定且与同前缀的别的操作不撞名
        var longName = AIToolNameConvention.Derive(new OperationContract("permission-condition-insight-query.get-permission-condition-batch-trial"));
        var sibling = AIToolNameConvention.Derive(new OperationContract("permission-condition-insight-query.get-permission-condition-batch-trials"));
        Assert.Equal(64, longName.Length);
        Assert.StartsWith("permission_condition_insight_query_get_permission_", longName, StringComparison.Ordinal);
        Assert.True(AIIdentifiers.IsValidToolName(longName));
        Assert.Equal(longName, AIToolNameConvention.Derive(new OperationContract("permission-condition-insight-query.get-permission-condition-batch-trial")));
        Assert.NotEqual(longName, sibling);
        Assert.True(AIIdentifiers.IsValidToolName("Orders_create-1") && !AIIdentifiers.IsValidToolName("has space") && !AIIdentifiers.IsValidToolName(""));
    }

    /// <summary>
    /// 启动失败链里第一个 AI 错误（宿主把特性启动失败包成 KERNEL_HOSTING 错误）。
    /// </summary>
    private static FrameworkException AIFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is FrameworkException framework && framework.Code.StartsWith("AI_", StringComparison.Ordinal))
            {
                return framework;
            }
        }

        throw new InvalidOperationException("No AI failure was found in the exception chain.", exception);
    }

    /// <summary>
    /// 启动：本地化 + 工具目录 + 回显操作 + 一个显式工具，并登记描述目录。
    /// </summary>
    private static Task<TestHost> StartAsync(params (string Key, string Value)[] configuration)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryLocalizationFeature>().UseFeature<AIToolsFeature>().UseFeature<EchoFeature>(),
            configuration.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)),
            static services =>
            {
                services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("echoes", typeof(EchoFeature)));
                services.AddAITool(AIFunctionFactory.Create((int a, int b) => a + b, "manual_add", "Add two numbers."));
            });
}

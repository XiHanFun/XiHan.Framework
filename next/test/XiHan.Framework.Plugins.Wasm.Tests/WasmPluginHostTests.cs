// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 以 Rust 写的 EchoWasmPlugin 验证发现、握手、调用、中间件、本地化、回调、事件、陷阱重建、截止时间与运行期加载。
/// </summary>
public sealed class WasmPluginHostTests
{
    [Fact]
    public async Task Feature_DiscoversPlugin_RegistersProxyOperations_AndInvokesThroughThePipeline()
    {
        await using var host = await EchoWasmPluginHost.StartAsync();
        var plugins = EchoWasmPluginHost.PluginHost(host);

        var status = plugins.Describe(EchoWasmPluginHost.PluginId);
        Assert.NotNull(status);
        Assert.Equal(LifecycleState.Ready, status.Status.State);
        Assert.Equal(1u, status.AbiVersion);
        Assert.Equal(1u, status.ProtocolVersion);
        Assert.False(status.Lazy);

        var registry = host.Services.GetRequiredService<OperationRegistry>();
        var descriptor = registry.Find("echoes.create").ValueOrThrow();
        Assert.Equal(EchoWasmPluginHost.PluginId, descriptor.OwnerId);
        Assert.True(descriptor.Contract.IsExposedTo(Expose.Http));
        Assert.NotNull(descriptor.Schema);
        Assert.Contains("WasmMiddleware[xihan.samples.echowasm:echo-guard]", host.App.DescribePipeline(), StringComparison.Ordinal);

        var response = await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":"ping"}""", new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        var payload = EchoWasmPluginHost.Payload(response);
        Assert.Equal("ping", payload.GetProperty("text").GetString());
        Assert.Equal(EchoWasmPluginHost.PluginId, payload.GetProperty("plugin").GetString());
        Assert.Equal("hello", payload.GetProperty("greeting").GetString());
        Assert.Equal("acme", payload.GetProperty("tenant").GetString());
        // observe 阶段的中间件在 before 阶段补了关联 ID，下游看到的是改写后的 Baggage
        Assert.StartsWith("wasm-", payload.GetProperty("correlation").GetString(), StringComparison.Ordinal);

        var health = host.App.Health();
        Assert.Equal(LifecycleState.Ready, health.State);
        Assert.Equal(LifecycleState.Ready, Assert.Single(health.Contributions, static c => c.Name == "plugins." + EchoWasmPluginHost.PluginId).State);
    }

    [Fact]
    public async Task Middleware_ShortCircuitsBlockedTenants_AndObservesOutcomes()
    {
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature<InMemoryLocalizationFeature>());

        var blocked = await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":"x"}""", new Dictionary<string, string> { [BaggageKeys.Tenant] = "blocked" });
        Assert.Equal("ECHOWASM_DENIED_001", blocked.Outcome.Error?.Code);
        Assert.Equal("租户 'blocked' 被回显守卫拦截。", host.Services.GetRequiredService<IMessageLocalizer>().Localize(blocked.Outcome.Error!, "zh-CN"));

        var allowed = await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":"y"}""");
        Assert.True(allowed.Outcome.IsSuccess, allowed.Outcome.ToString());

        var stats = EchoWasmPluginHost.Payload(await EchoWasmPluginHost.InvokeAsync(host, "events.list", "{}"));
        // 三次调用都经过 before；短路的那次没有 after，events.list 自己的 after 在应答之后才计数
        Assert.Equal(3, stats.GetProperty("middlewareBefore").GetInt32());
        Assert.Equal(1, stats.GetProperty("middlewareAfter").GetInt32());
    }

    [Fact]
    public async Task PluginError_IsTranslatedWithTheHandshakeCatalog()
    {
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature<InMemoryLocalizationFeature>());

        var response = await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":""}""");

        Assert.Equal("ECHOWASM_VALIDATION_001", response.Outcome.Error?.Code);
        Assert.Equal("文本不能为空。", host.Services.GetRequiredService<IMessageLocalizer>().Localize(response.Outcome.Error!, "zh-CN"));
        Assert.Equal(KernelErrors.UnknownOperation, (await EchoWasmPluginHost.InvokeAsync(host, "echoes.relay", """{"operation":"pings.missing","payload":{}}""")).Outcome.Error?.Code);
    }

    [Fact]
    public async Task Callback_RunsWithPluginIdentity()
    {
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature(new StubFeature(TestManifests.Create("xihan.app.pings"))
        {
            OnConfigure = static ctx => ctx.AddOperation<PingOperation, PingRequest, PingResponse>(new OperationContract("pings.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
        }));

        var relayed = await EchoWasmPluginHost.InvokeAsync(host, "echoes.relay", """{"operation":"pings.create","payload":{"name":"from-wasm"}}""");

        Assert.True(relayed.Outcome.IsSuccess, relayed.Outcome.ToString());
        var payload = EchoWasmPluginHost.Payload(relayed);
        Assert.Equal("from-wasm", payload.GetProperty("name").GetString());
        Assert.Equal(EchoWasmPluginHost.PluginId, payload.GetProperty("principal").GetString());
        Assert.Equal(WasmHostFunctions.PluginScheme, payload.GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task Trap_MarksFailed_DegradesTheApp_AndRebuildsTheInstance()
    {
        await using var host = await EchoWasmPluginHost.StartAsync();
        var plugins = EchoWasmPluginHost.PluginHost(host);

        var trapped = await EchoWasmPluginHost.InvokeAsync(host, "echoes.trap", "{}");
        Assert.Equal(PluginErrors.Crashed, trapped.Outcome.Error?.Code);

        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => plugins.Describe(EchoWasmPluginHost.PluginId)!.Status.State == LifecycleState.Failed, TimeSpan.FromSeconds(5)));
        Assert.Equal(LifecycleState.Degraded, host.App.State);
        Assert.True(host.App.Health().Ready);

        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => plugins.Describe(EchoWasmPluginHost.PluginId)!.Status.State == LifecycleState.Ready, TimeSpan.FromSeconds(15)));
        Assert.Equal(1, plugins.Describe(EchoWasmPluginHost.PluginId)!.Restarts);
        Assert.Equal(LifecycleState.Ready, host.App.State);
        Assert.True((await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":"again"}""")).Outcome.IsSuccess);
    }

    [Fact]
    public async Task Deadline_InterruptsTheGuest()
    {
        await using var host = await EchoWasmPluginHost.StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, deadline: DateTimeOffset.UtcNow.AddMilliseconds(300));

        var started = DateTime.UtcNow;
        var response = await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest("echoes.spin", Encoding.UTF8.GetBytes("""{"millis":10000}""")));

        Assert.Equal(KernelErrors.DeadlineExceeded, response.Outcome.Error?.Code);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5), "the guest must be interrupted at the deadline instead of running to completion");
        var plugins = EchoWasmPluginHost.PluginHost(host);
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => plugins.Describe(EchoWasmPluginHost.PluginId)!.Status.State == LifecycleState.Ready, TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public async Task Events_AreDeliveredToSubscribedPlugins()
    {
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>());
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var published = await publisher.PublishAsync("echo.events", new MessageEnvelope { Id = "evt-wasm-1", Source = "tests", Type = "echo.created", Data = "{}"u8.ToArray() }, CancellationToken.None);
        Assert.True(published.IsSuccess, published.ToString());

        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() =>
        {
            var listed = EchoWasmPluginHost.InvokeAsync(host, "events.list", "{}").GetAwaiter().GetResult();
            return listed.Outcome.IsSuccess && EchoWasmPluginHost.Payload(listed).GetProperty("ids").EnumerateArray().Any(static id => id.GetString() == "evt-wasm-1");
        }, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task LoadAsync_AtRuntime_RejectsMiddlewareContributions()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        var loaded = await plugins.LoadAsync(new PluginSource(EchoWasmPluginHost.Directory, new PermissionGrant(["capability:kernel.pipeline"])), CancellationToken.None);

        Assert.Equal(PluginErrors.InvalidModule, loaded.Error?.Code);
        Assert.Empty(plugins.Plugins);
    }

    [Fact]
    public void Options_FromConfiguration_ParseLimitsMountsAndTimings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Plugins:Wasm:Directories:0"] = "plugins/a",
            ["XiHan:Plugins:Wasm:Grants:xihan.samples.echowasm:0"] = "capability:kernel.pipeline",
            ["XiHan:Plugins:Wasm:Mounts:xihan.samples.echowasm:/data"] = "C:/data",
            ["XiHan:Plugins:Wasm:MemoryLimitBytes"] = "1048576",
            ["XiHan:Plugins:Wasm:EpochIntervalMilliseconds"] = "2",
            ["XiHan:Plugins:Wasm:MiddlewareTimeoutMilliseconds"] = "250",
            ["XiHan:Plugins:Wasm:RestartLimit"] = "0",
        }).Build();

        var options = WasmPluginOptions.FromConfiguration(configuration);

        Assert.Equal(["plugins/a"], options.Directories);
        Assert.True(options.GrantsFor("xihan.samples.echowasm").IsGranted("capability:kernel.pipeline"));
        Assert.Equal("C:/data", options.MountsFor("xihan.samples.echowasm")["/data"]);
        Assert.Equal(1048576, options.MemoryLimitBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(2), options.EpochInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.MiddlewareTimeout);
        Assert.Equal(0, options.RestartLimit);
        Assert.Equal(TimeSpan.FromSeconds(30), options.CallTimeout);
        Assert.Throws<ArgumentException>(() => WasmPluginOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Plugins:Wasm:MemoryLimitBytes"] = "-1" }).Build()));
    }
}

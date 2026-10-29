// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 以 Go 写的 echo-go 子进程验证 .NET 宿主加载第二语言插件（P4-06）。
/// </summary>
/// <remarks>
/// 发现、握手、经管道调用、本地化、回调身份、事件、崩溃重启与停止。
/// </remarks>
public sealed class EchoGoPluginHostTests
{
    [Fact]
    public async Task Feature_DiscoversTheGoPlugin_RegistersProxyOperations_AndInvokesThroughThePipeline()
    {
        await using var host = await EchoGoPluginHost.StartAsync();
        var plugins = EchoPluginHost.PluginHost(host);

        var status = plugins.Describe(EchoGoPluginHost.PluginId);
        Assert.NotNull(status);
        Assert.Equal(LifecycleState.Ready, status.Status.State);
        Assert.Equal(1u, status.ProtocolVersion);
        Assert.NotNull(status.ProcessId);

        var registry = host.Services.GetRequiredService<OperationRegistry>();
        var descriptor = registry.Find("echoes.create").ValueOrThrow();
        Assert.Equal(EchoGoPluginHost.PluginId, descriptor.OwnerId);
        Assert.True(descriptor.Contract.IsExposedTo(Expose.Http));
        Assert.True(descriptor.Contract.IsExposedTo(Expose.Mcp));
        Assert.False(descriptor.Contract.IsExposedTo(Expose.Grpc));
        Assert.NotNull(descriptor.Schema);
        Assert.Equal(Stability.Experimental, descriptor.Contract.Stability);

        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"ping"}""", new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme", [BaggageKeys.Correlation] = "corr-1" });
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        var payload = EchoPluginHost.Payload(response);
        Assert.Equal("ping", payload.GetProperty("text").GetString());
        Assert.Equal("hello", payload.GetProperty("greeting").GetString());
        Assert.Equal("acme", payload.GetProperty("tenant").GetString());
        Assert.Equal("corr-1", payload.GetProperty("correlation").GetString());

        var health = host.App.Health();
        Assert.Equal(LifecycleState.Ready, health.State);
        var contribution = Assert.Single(health.Contributions, static c => c.Name == "plugins." + EchoGoPluginHost.PluginId);
        Assert.Equal(LifecycleState.Ready, contribution.State);
    }

    [Fact]
    public async Task PluginError_IsTranslatedWithTheHandshakeCatalog()
    {
        await using var host = await EchoGoPluginHost.StartAsync(static b => b.UseFeature<InMemoryLocalizationFeature>());

        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":""}""");

        Assert.True(response.Outcome.IsFailure);
        Assert.Equal("ECHOGO_VALIDATION_002", response.Outcome.Error?.Code);
        Assert.Equal("echogo.text.missing", response.Outcome.Error?.MessageKey);
        var localizer = host.Services.GetRequiredService<IMessageLocalizer>();
        Assert.Equal("echoes.create 的文本不能为空。", localizer.Localize(response.Outcome.Error!, "zh-CN"));
        Assert.Equal("The text of echoes.create is required.", localizer.Localize(response.Outcome.Error!, "en-US"));
    }

    [Fact]
    public async Task Callback_RunsWithPluginIdentity_AndRequiresTheGrantedPermission()
    {
        await using var granted = await EchoGoPluginHost.StartAsync(static b => b.UseFeature(PingFeature()));
        var relayed = await EchoPluginHost.InvokeAsync(granted, "echoes.relay", """{"operation":"pings.create","payload":{"name":"from-go"}}""");
        Assert.True(relayed.Outcome.IsSuccess, relayed.Outcome.ToString());
        var payload = EchoPluginHost.Payload(relayed);
        Assert.Equal("from-go", payload.GetProperty("name").GetString());
        Assert.Equal(EchoGoPluginHost.PluginId, payload.GetProperty("principal").GetString());
        Assert.Equal(OutOfProcessPluginHost.PluginScheme, payload.GetProperty("scheme").GetString());

        var unknown = await EchoPluginHost.InvokeAsync(granted, "echoes.relay", """{"operation":"pings.missing","payload":{}}""");
        Assert.Equal(KernelErrors.UnknownOperation, unknown.Outcome.Error?.Code);

        await using var denied = await EchoGoPluginHost.StartAsync(grantPipeline: false);
        Assert.Null(EchoPluginHost.PluginHost(denied).Describe(EchoGoPluginHost.PluginId));
        Assert.False(denied.Services.GetRequiredService<OperationRegistry>().Find("echoes.create").HasValue);
    }

    [Fact]
    public async Task Events_AreDeliveredToTheGoPlugin()
    {
        await using var host = await EchoGoPluginHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>());
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var published = await publisher.PublishAsync("echo.events", new MessageEnvelope { Id = "go-evt-1", Source = "tests", Type = "echo.created", Data = "{}"u8.ToArray() }, CancellationToken.None);
        Assert.True(published.IsSuccess, published.ToString());

        Assert.True(await EchoPluginHost.WaitUntilAsync(() =>
        {
            var listed = EchoPluginHost.InvokeAsync(host, "events.list", "{}").GetAwaiter().GetResult();
            return listed.Outcome.IsSuccess && EchoPluginHost.Payload(listed).GetProperty("ids").EnumerateArray().Any(static id => id.GetString() == "go-evt-1");
        }, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Crash_MarksFailed_DegradesTheApp_AndRestartsWithBackoff()
    {
        await using var host = await EchoGoPluginHost.StartAsync();
        var plugins = EchoPluginHost.PluginHost(host);
        var firstProcess = plugins.Describe(EchoGoPluginHost.PluginId)!.ProcessId;

        var crashed = await EchoPluginHost.InvokeAsync(host, "echoes.crash", "{}");
        Assert.True(crashed.Outcome.IsFailure);

        Assert.True(await EchoPluginHost.WaitUntilAsync(() => plugins.Describe(EchoGoPluginHost.PluginId)!.Status.State == LifecycleState.Failed, TimeSpan.FromSeconds(10)));
        Assert.Equal(PluginErrors.Crashed, plugins.Describe(EchoGoPluginHost.PluginId)!.Status.Error?.Code);
        Assert.Equal(LifecycleState.Degraded, host.App.State);

        Assert.True(await EchoPluginHost.WaitUntilAsync(() => plugins.Describe(EchoGoPluginHost.PluginId)!.Status.State == LifecycleState.Ready, TimeSpan.FromSeconds(20)));
        var restarted = plugins.Describe(EchoGoPluginHost.PluginId)!;
        Assert.Equal(1, restarted.Restarts);
        Assert.NotEqual(firstProcess, restarted.ProcessId);
        Assert.Equal(LifecycleState.Ready, host.App.State);
        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"again"}""");
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
    }

    [Fact]
    public async Task Stop_TerminatesTheGoProcess()
    {
        var host = await EchoGoPluginHost.StartAsync();
        var processId = EchoPluginHost.PluginHost(host).Describe(EchoGoPluginHost.PluginId)!.ProcessId!.Value;
        Assert.True(IsRunning(processId));

        await host.DisposeAsync();

        Assert.True(await EchoPluginHost.WaitUntilAsync(() => !IsRunning(processId), TimeSpan.FromSeconds(10)));
    }

    /// <summary>
    /// 登记宿主侧操作 pings.create 的桩特性。
    /// </summary>
    private static StubFeature PingFeature() => new(TestManifests.Create("xihan.app.pings"))
    {
        OnConfigure = static ctx => ctx.AddOperation<PingOperation, PingRequest, PingResponse>(new OperationContract("pings.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
    };

    /// <summary>
    /// 进程是否仍在运行。
    /// </summary>
    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

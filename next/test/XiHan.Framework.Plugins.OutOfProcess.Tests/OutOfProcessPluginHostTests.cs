// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
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
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 进程外插件宿主的端到端用例。
/// </summary>
/// <remarks>
/// 以真实的 EchoPlugin 子进程验证发现、握手、调用、本地化、回调、事件、崩溃重启、停止与运行期加载。
/// </remarks>
public sealed class OutOfProcessPluginHostTests
{
    [Fact]
    public async Task Feature_DiscoversPlugin_RegistersProxyOperations_AndInvokesThroughThePipeline()
    {
        await using var host = await EchoPluginHost.StartAsync();
        var plugins = EchoPluginHost.PluginHost(host);

        var status = plugins.Describe(EchoPluginHost.PluginId);
        Assert.NotNull(status);
        Assert.Equal(LifecycleState.Ready, status.Status.State);
        Assert.Equal(1u, status.ProtocolVersion);
        Assert.NotNull(status.ProcessId);
        Assert.False(status.Lazy);

        var registry = host.Services.GetRequiredService<OperationRegistry>();
        var descriptor = registry.Find("echoes.create").ValueOrThrow();
        Assert.Equal(EchoPluginHost.PluginId, descriptor.OwnerId);
        Assert.True(descriptor.Contract.IsExposedTo(Expose.Http));
        Assert.False(descriptor.Contract.IsExposedTo(Expose.Grpc));
        Assert.NotNull(descriptor.Schema);
        Assert.Equal(Stability.Internal, registry.Find("echoes.crash").ValueOrThrow().Contract.Stability);

        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"ping"}""", new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        var payload = EchoPluginHost.Payload(response);
        Assert.Equal("ping", payload.GetProperty("text").GetString());
        Assert.Equal(EchoPluginHost.PluginId, payload.GetProperty("plugin").GetString());
        Assert.Equal("hello", payload.GetProperty("greeting").GetString());
        Assert.Equal("acme", payload.GetProperty("tenant").GetString());

        var health = host.App.Health();
        Assert.Equal(LifecycleState.Ready, health.State);
        var contribution = Assert.Single(health.Contributions, static c => c.Name == "plugins." + EchoPluginHost.PluginId);
        Assert.Equal(LifecycleState.Ready, contribution.State);
    }

    [Fact]
    public async Task PluginError_IsTranslatedWithTheHandshakeCatalog()
    {
        await using var host = await EchoPluginHost.StartAsync(static b => b.UseFeature<InMemoryLocalizationFeature>());

        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":""}""");

        Assert.True(response.Outcome.IsFailure);
        Assert.Equal("ECHO_VALIDATION_001", response.Outcome.Error?.Code);
        Assert.Equal("echo.text.empty", response.Outcome.Error?.MessageKey);
        var localizer = host.Services.GetRequiredService<IMessageLocalizer>();
        Assert.Equal("文本不能为空。", localizer.Localize(response.Outcome.Error!, "zh-CN"));
        Assert.Equal("The text must not be empty.", localizer.Localize(response.Outcome.Error!, "en-US"));
    }

    [Fact]
    public async Task Callback_RunsWithPluginIdentity_AndRequiresTheGrantedPermission()
    {
        await using var granted = await EchoPluginHost.StartAsync(static b => b.UseFeature(PingFeature()));
        var relayed = await EchoPluginHost.InvokeAsync(granted, "echoes.relay", """{"operation":"pings.create","payload":{"name":"from-plugin"}}""");
        Assert.True(relayed.Outcome.IsSuccess, relayed.Outcome.ToString());
        var payload = EchoPluginHost.Payload(relayed);
        Assert.Equal("from-plugin", payload.GetProperty("name").GetString());
        Assert.Equal(EchoPluginHost.PluginId, payload.GetProperty("principal").GetString());
        Assert.Equal(OutOfProcessPluginHost.PluginScheme, payload.GetProperty("scheme").GetString());

        var unknown = await EchoPluginHost.InvokeAsync(granted, "echoes.relay", """{"operation":"pings.missing","payload":{}}""");
        Assert.Equal(KernelErrors.UnknownOperation, unknown.Outcome.Error?.Code);

        // 清单声明了 capability:kernel.pipeline 却没有授予：发现阶段就被拒绝，插件以 Failed 上报
        await using var denied = await EchoPluginHost.StartAsync(grantPipeline: false);
        var status = EchoPluginHost.PluginHost(denied).Describe(EchoPluginHost.PluginId);
        Assert.Null(status);
        Assert.Equal(LifecycleState.Ready, denied.App.State);
        Assert.False(denied.Services.GetRequiredService<OperationRegistry>().Find("echoes.create").HasValue);
    }

    [Fact]
    public async Task Deadline_IsPropagatedToThePlugin()
    {
        await using var host = await EchoPluginHost.StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, deadline: DateTimeOffset.UtcNow.AddMilliseconds(300));

        var response = await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest("echoes.create", Encoding.UTF8.GetBytes("""{"text":"slow","delayMs":5000}""")));

        Assert.Equal(KernelErrors.DeadlineExceeded, response.Outcome.Error?.Code);
    }

    [Fact]
    public async Task Events_AreDeliveredToSubscribedPlugins()
    {
        await using var host = await EchoPluginHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>());
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var published = await publisher.PublishAsync("echo.events", new MessageEnvelope { Id = "evt-1", Source = "tests", Type = "echo.created", Data = "{}"u8.ToArray() }, CancellationToken.None);
        Assert.True(published.IsSuccess, published.ToString());

        Assert.True(await EchoPluginHost.WaitUntilAsync(() =>
        {
            var listed = EchoPluginHost.InvokeAsync(host, "events.list", "{}").GetAwaiter().GetResult();
            return listed.Outcome.IsSuccess && EchoPluginHost.Payload(listed).GetProperty("ids").EnumerateArray().Any(static id => id.GetString() == "evt-1");
        }, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Crash_MarksFailed_DegradesTheApp_AndRestartsWithBackoff()
    {
        await using var host = await EchoPluginHost.StartAsync();
        var plugins = EchoPluginHost.PluginHost(host);
        var firstProcess = plugins.Describe(EchoPluginHost.PluginId)!.ProcessId;

        var crashed = await EchoPluginHost.InvokeAsync(host, "echoes.crash", "{}");
        Assert.True(crashed.Outcome.IsFailure);

        Assert.True(await EchoPluginHost.WaitUntilAsync(() => plugins.Describe(EchoPluginHost.PluginId)!.Status.State == LifecycleState.Failed, TimeSpan.FromSeconds(10)));
        var failed = plugins.Describe(EchoPluginHost.PluginId)!;
        Assert.Equal(PluginErrors.Crashed, failed.Status.Error?.Code);
        Assert.Equal(LifecycleState.Degraded, host.App.State);
        Assert.True(host.App.Health().Ready);

        // 退避 1 秒后重启：新进程、累计重启次数 1、应用回到 Ready
        Assert.True(await EchoPluginHost.WaitUntilAsync(() => plugins.Describe(EchoPluginHost.PluginId)!.Status.State == LifecycleState.Ready, TimeSpan.FromSeconds(20)));
        var restarted = plugins.Describe(EchoPluginHost.PluginId)!;
        Assert.Equal(1, restarted.Restarts);
        Assert.NotEqual(firstProcess, restarted.ProcessId);
        Assert.Equal(LifecycleState.Ready, host.App.State);
        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"again"}""");
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
    }

    [Fact]
    public async Task Stop_TerminatesThePluginProcess()
    {
        var host = await EchoPluginHost.StartAsync();
        var processId = EchoPluginHost.PluginHost(host).Describe(EchoPluginHost.PluginId)!.ProcessId!.Value;
        Assert.True(IsRunning(processId));

        await host.DisposeAsync();

        Assert.True(await EchoPluginHost.WaitUntilAsync(() => !IsRunning(processId), TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task LoadAndUnload_AtRuntime_UseThePluginHostContract()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<OutOfProcessPluginsFeature>(), [new("XiHan:Plugins:OutOfProcess:HealthIntervalSeconds", "1")]);
        var plugins = host.Services.GetRequiredService<IPluginHost>();
        var source = new PluginSource(EchoPluginHost.Directory, new PermissionGrant(["capability:kernel.pipeline"]));

        var loaded = await plugins.LoadAsync(source, CancellationToken.None);
        Assert.Equal(LifecycleState.Ready, loaded.ValueOrThrow().State);
        Assert.Equal(PluginErrors.AlreadyLoaded, (await plugins.LoadAsync(source, CancellationToken.None)).Error?.Code);

        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"late"}""");
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());

        Assert.True((await plugins.UnloadAsync(EchoPluginHost.PluginId, CancellationToken.None)).IsSuccess);
        Assert.Empty(plugins.Plugins);
        Assert.Equal(PluginErrors.NotFound, (await plugins.UnloadAsync(EchoPluginHost.PluginId, CancellationToken.None)).Error?.Code);
        Assert.Equal(PluginErrors.Unavailable, (await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"gone"}""")).Outcome.Error?.Code);
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

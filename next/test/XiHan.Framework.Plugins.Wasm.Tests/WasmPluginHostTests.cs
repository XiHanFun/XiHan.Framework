// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// Wasm 插件宿主的端到端用例。
/// </summary>
/// <remarks>
/// 以 Rust 写的 EchoWasmPlugin 验证发现、握手、调用、中间件、本地化、回调、事件、陷阱重建、截止时间与运行期加载。
/// </remarks>
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
    public async Task Middleware_IsOrderedByTheManifestBeforeAndAfter()
    {
        // 基准次序：宿主的 Marker（归属 xihan.app.marker 排在 xihan.tests.wat 前）、first、second（清单顺序）。
        // first 声明在本插件的 second 之后，second 声明在宿主中间件（类型全名）之前：结果 second、Marker、first
        using var plugin = WatPlugin.WithMiddleware(new JsonArray(
            new JsonObject { ["middleware"] = "first", ["stage"] = "observe", ["after"] = new JsonArray("second") },
            new JsonObject { ["middleware"] = "second", ["stage"] = "observe", ["before"] = new JsonArray(typeof(MarkerMiddleware).FullName) }));
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<WasmPluginsFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.marker"))
            {
                OnConfigure = static ctx => ctx.Pipeline.UseInstance(new MarkerMiddleware(), PipelineStage.Observe, owner: "xihan.app.marker"),
            }),
            [new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory)]);
        Assert.Equal(LifecycleState.Ready, EchoWasmPluginHost.PluginHost(host).Describe(WatPlugin.PluginId)!.Status.State);

        var observe = host.App.DescribePipeline().Split('\n').Where(static line => line.StartsWith("observe ", StringComparison.Ordinal)).Select(static line => line[13..]).ToList();

        Assert.Equal(
            [
                $"{typeof(WasmMiddleware).FullName}[{WatPlugin.PluginId}:second]",
                typeof(MarkerMiddleware).FullName,
                $"{typeof(WasmMiddleware).FullName}[{WatPlugin.PluginId}:first]",
            ],
            observe.Where(static line => !line.StartsWith("XiHan.Framework.Kernel.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Middleware_ReferencingAnUnknownMiddleware_FailsThePipelineBuild()
    {
        using var plugin = WatPlugin.WithMiddleware(new JsonArray(new JsonObject { ["middleware"] = "guard", ["stage"] = "observe", ["before"] = new JsonArray("xihan.other:missing") }));

        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>(), [new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory)]);

        // 与按类型声明的约束一样，目标在排序时解析：构建或打印管道时以配置错误失败
        var exception = Assert.Throws<FrameworkException>(() => host.App.DescribePipeline());
        Assert.Equal(KernelErrors.PipelineRegistration, exception.Code);
        Assert.Contains("xihan.other:missing", exception.Message, StringComparison.Ordinal);
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
    public async Task Discovery_KeepsARejectedPluginAsFailed_AndReportsItsHealth()
    {
        // 清单声明了 capability:kernel.pipeline 却没有授予：发现时被拒、不实例化，以 Failed 登记（lifecycle.md §1）
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>(), [new("XiHan:Plugins:Wasm:Directories:0", EchoWasmPluginHost.Directory)]);
        var plugins = EchoWasmPluginHost.PluginHost(host);

        var status = plugins.Describe(EchoWasmPluginHost.PluginId);
        Assert.NotNull(status);
        Assert.Equal(LifecycleState.Failed, status.Status.State);
        Assert.Equal(PluginErrors.PermissionDenied, status.Status.Error?.Code);
        Assert.Null(status.AbiVersion);
        Assert.Equal(LifecycleState.Degraded, host.App.State);
        Assert.Equal(LifecycleState.Failed, Assert.Single(host.App.Health().Contributions, static c => c.Name == "plugins." + EchoWasmPluginHost.PluginId).State);
        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find("echoes.create").HasValue);
        Assert.DoesNotContain("WasmMiddleware", host.App.DescribePipeline(), StringComparison.Ordinal);
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
    public async Task LoadAsync_AtRuntime_RejectsOperationsRequiringUnregisteredPermissions()
    {
        using var guarded = WatPlugin.Guarded("wat.call-permission");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        // 装配完成之后登记：权限码没登记，插件整体登记失败，不留下代理操作
        var loaded = await plugins.LoadAsync(new PluginSource(guarded.Directory, new PermissionGrant([])), CancellationToken.None);

        Assert.Equal(KernelErrors.UnregisteredPermission, loaded.Error?.Code);
        Assert.Empty(plugins.Plugins);
        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).HasValue);
    }

    [Fact]
    public async Task LoadAsync_AtRuntime_RegistersOperationsRequiringRegisteredPermissions()
    {
        using var guarded = WatPlugin.Guarded("wat.call-permission");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.permissions"))
        {
            OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("wat.call-permission", "Call the WAT plugin")),
        }));
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        var loaded = await plugins.LoadAsync(new PluginSource(guarded.Directory, new PermissionGrant([])), CancellationToken.None);

        Assert.True(loaded.IsSuccess, loaded.ToString());
        Assert.Equal("wat.call-permission", host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).ValueOrThrow().Contract.Permission);
    }

    [Fact]
    public async Task DiscoveredPlugin_RequiringAnUnregisteredPermission_FailsTheAssembly()
    {
        using var guarded = WatPlugin.Guarded("wat.call-permission");

        // 配置阶段发现的插件随应用一起装配：与应用自己的操作一样，在装配完成时核对，启动失败
        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>(), [new("XiHan:Plugins:Wasm:Directories:0", guarded.Directory)]));

        Assert.Equal(KernelErrors.UnregisteredPermission, exception.Code);
        Assert.Contains("'wat.call' of 'xihan.tests.wat' requires 'wat.call-permission'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_AtRuntime_RegistersTheCodesThePluginDeclares()
    {
        // 清单声明了 wat.call-permission 与 wat.audit：宿主替插件登记，操作引用自己声明的码照常登记（ADR-0080）
        using var declaring = WatPlugin.Declaring(["wat.call-permission", "wat.audit"], "wat.call-permission");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        var loaded = await plugins.LoadAsync(new PluginSource(declaring.Directory, new PermissionGrant([])), CancellationToken.None);

        Assert.True(loaded.IsSuccess, loaded.ToString());
        var permissions = host.Services.GetRequiredService<PermissionRegistry>();
        Assert.Equal("wat.call-permission", permissions.Find("wat.call-permission").ValueOrThrow().DisplayName);
        Assert.True(permissions.Find("wat.audit").HasValue);
        Assert.Equal("wat.call-permission", host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).ValueOrThrow().Contract.Permission);
    }

    [Fact]
    public async Task LoadAsync_AtRuntime_OnlyRegistersDeclaredCodes_AndRejectsTheRestAsBefore()
    {
        // 声明了 wat.audit，操作却要 wat.call-permission：没声明的仍按未登记拒绝，声明的码也撤回
        using var declaring = WatPlugin.Declaring(["wat.audit"], "wat.call-permission");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        var loaded = await plugins.LoadAsync(new PluginSource(declaring.Directory, new PermissionGrant([])), CancellationToken.None);

        Assert.Equal(KernelErrors.UnregisteredPermission, loaded.Error?.Code);
        Assert.Empty(plugins.Plugins);
        Assert.False(host.Services.GetRequiredService<PermissionRegistry>().Find("wat.audit").HasValue);
        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).HasValue);
    }

    [Fact]
    public async Task LoadAsync_AtRuntime_RejectsADeclaredCodeTheHostAlreadyDefines()
    {
        // 宿主特性定义了 wat.call-permission，插件又声明它：冲突，插件整体被拒，宿主的定义不变
        using var declaring = WatPlugin.Declaring(["wat.audit", "wat.call-permission"], "wat.call-permission");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.permissions"))
        {
            OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("wat.call-permission", "Call the WAT plugin")),
        }));
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        var loaded = await plugins.LoadAsync(new PluginSource(declaring.Directory, new PermissionGrant([])), CancellationToken.None);

        Assert.Equal(KernelErrors.DuplicatePermission, loaded.Error?.Code);
        Assert.Equal("wat.call-permission", loaded.Error?.Details?["code"]);
        Assert.Empty(plugins.Plugins);
        var permissions = host.Services.GetRequiredService<PermissionRegistry>();
        Assert.Equal("Call the WAT plugin", permissions.Find("wat.call-permission").ValueOrThrow().DisplayName);
        Assert.False(permissions.Find("wat.audit").HasValue);
        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).HasValue);
    }

    [Fact]
    public async Task LoadAsync_AtRuntime_RejectsTheSecondPluginDeclaringTheSameCode()
    {
        using var first = WatPlugin.Declaring(["wat.shared"], "wat.shared", "xihan.tests.wat-a", "wat-a.call");
        using var second = WatPlugin.Declaring(["wat.shared"], "wat.shared", "xihan.tests.wat-b", "wat-b.call");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        Assert.True((await plugins.LoadAsync(new PluginSource(first.Directory, new PermissionGrant([])), CancellationToken.None)).IsSuccess);
        var loaded = await plugins.LoadAsync(new PluginSource(second.Directory, new PermissionGrant([])), CancellationToken.None);

        // 后登记的那个整体失败；先登记的不受影响
        Assert.Equal(KernelErrors.DuplicatePermission, loaded.Error?.Code);
        Assert.Equal("xihan.tests.wat-a", Assert.Single(plugins.Plugins).Manifest.Id);
        var registry = host.Services.GetRequiredService<OperationRegistry>();
        Assert.True(registry.Find("wat-a.call").HasValue);
        Assert.False(registry.Find("wat-b.call").HasValue);
    }

    [Fact]
    public async Task DiscoveredPlugin_DeclaringItsCodes_PassesTheAssemblyCheck()
    {
        using var declaring = WatPlugin.Declaring(["wat.call-permission"], "wat.call-permission");

        // 配置阶段发现时就登记了声明的码：装配完成时的核对看得到它
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>(), [new("XiHan:Plugins:Wasm:Directories:0", declaring.Directory)]);

        Assert.True(host.Services.GetRequiredService<PermissionRegistry>().Find("wat.call-permission").HasValue);
        Assert.Equal("wat.call-permission", host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).ValueOrThrow().Contract.Permission);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DiscoveredPlugin_DeclaringACodeTheHostDefines_FailsTheAssembly(bool hostFirst)
    {
        using var declaring = WatPlugin.Declaring(["wat.call-permission"], null);
        var permissions = new StubFeature(TestManifests.Create("xihan.app.permissions"))
        {
            OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("wat.call-permission", "Call the WAT plugin")),
        };

        // 不论宿主特性先登记还是插件先登记，同一个码都是冲突，启动失败
        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            b =>
            {
                if (hostFirst)
                {
                    b.UseFeature(permissions).UseFeature<WasmPluginsFeature>();
                }
                else
                {
                    b.UseFeature<WasmPluginsFeature>().UseFeature(permissions);
                }
            },
            [new("XiHan:Plugins:Wasm:Directories:0", declaring.Directory)]));

        Assert.Equal(KernelErrors.DuplicatePermission, exception.Code);
        Assert.Contains("wat.call-permission", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoveredPlugin_PolicyOnItsOwnDeclaredCode_PassesTheAuthorizationStartCheck()
    {
        // 插件声明的码在配置阶段登记，授权能力启动时核对策略：permission:wat.audit 此时已可用（ADR-0080）
        using var declaring = WatPlugin.Declaring(["wat.audit"], null, policy: "permission:wat.audit");

        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<WasmPluginsFeature>(), [new("XiHan:Plugins:Wasm:Directories:0", declaring.Directory)]);

        Assert.Equal("permission:wat.audit", host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).ValueOrThrow().Contract.Policy);
    }

    [Fact]
    public async Task DiscoveredPlugin_ReferencingAMissingPolicy_FailsTheStart()
    {
        using var declaring = WatPlugin.Declaring(["wat.audit"], null, policy: "auditors");

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<WasmPluginsFeature>(), [new("XiHan:Plugins:Wasm:Directories:0", declaring.Directory)]));

        // 授权能力的启动核对：配置阶段发现的插件的操作一并核对，策略不存在即终止启动
        Assert.Equal(SecurityErrors.UnregisteredPolicy, exception.Code);
        Assert.Contains("'wat.call' of 'xihan.tests.wat' requires policy 'auditors'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_AfterTheAuthorizationStart_RejectsAMissingPolicy_AndKeepsNoDeclaredCode()
    {
        using var declaring = WatPlugin.Declaring(["wat.audit"], null, policy: "auditors");
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        // 启动之后登记：策略不存在，整组被拒，声明的码撤回
        var loaded = await plugins.LoadAsync(new PluginSource(declaring.Directory, new PermissionGrant([])), CancellationToken.None);

        Assert.Equal(SecurityErrors.UnregisteredPolicy, loaded.Error?.Code);
        Assert.Equal("auditors", loaded.Error?.Details?["policy"]);
        Assert.Empty(plugins.Plugins);
        Assert.False(host.Services.GetRequiredService<PermissionRegistry>().Find("wat.audit").HasValue);
        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).HasValue);
    }

    [Theory]
    [InlineData("Wat.Audit")]
    [InlineData("wat")]
    [InlineData("wat.audit,wat.audit")]
    public async Task LoadAsync_AtRuntime_RejectsAManifestWithMalformedOrDuplicateDeclaredCodes(string declared)
    {
        // 清单读不出（不合文法、重复）：按发现时被拒，什么都不登记
        using var declaring = WatPlugin.Declaring(declared.Split(','), null);
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        var loaded = await plugins.LoadAsync(new PluginSource(declaring.Directory, new PermissionGrant([])), CancellationToken.None);

        Assert.Equal(PluginErrors.InvalidSource, loaded.Error?.Code);
        Assert.Contains("contributes.permissions", loaded.Error?.ToString(), StringComparison.Ordinal);
        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find(WatPlugin.Operation).HasValue);
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

    [Theory]
    [InlineData("MemoryLimitBytes", "0", "a positive integer")]
    [InlineData("MemoryLimitBytes", "1.5", "a positive integer")]
    [InlineData("StackLimitBytes", "3000000000", "a positive integer not greater than 2147483647")]
    [InlineData("EpochIntervalMilliseconds", "0", "a positive number")]
    [InlineData("CallTimeoutSeconds", "0", "a positive number")]
    [InlineData("MiddlewareTimeoutMilliseconds", "0", "a positive number")]
    [InlineData("HealthIntervalSeconds", "0", "a positive number")]
    [InlineData("RestartWindowSeconds", "0", "a positive number")]
    [InlineData("HealthFailures", "0", "a positive integer")]
    [InlineData("HealthFailures", "2.5", "a positive integer")]
    [InlineData("RestartLimit", "-1", "a non-negative integer")]
    public void Options_FromConfiguration_RejectWhatThePropertiesReject_WithTheConfigurationKey(string key, string value, string expected)
    {
        // 配置与属性的取值范围一致：0 等不合规的值以带配置键的 ArgumentException 拒绝，不再漏到属性赋值时抛出不带键的 ArgumentOutOfRangeException
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"{WasmPluginOptions.ConfigurationSection}:{key}"] = value }).Build();

        var exception = Assert.Throws<ArgumentException>(() => WasmPluginOptions.FromConfiguration(configuration));

        Assert.Contains($"{WasmPluginOptions.ConfigurationSection}:{key} must be {expected}", exception.Message, StringComparison.Ordinal);
    }
}

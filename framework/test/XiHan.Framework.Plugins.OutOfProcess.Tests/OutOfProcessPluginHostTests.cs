// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
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
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 进程外插件宿主的端到端用例
/// </summary>
/// <remarks>
/// 以真实的 EchoPlugin 子进程验证发现、握手、调用、本地化、回调、事件、崩溃重启、停止与运行期加载
/// </remarks>
public sealed class OutOfProcessPluginHostTests
{
    [Fact]
    public async Task Feature_DiscoversPlugin_RegistersProxyOperations_AndInvokesThroughThePipeline()
    {
        await using var host = await EchoPluginHost.StartAsync();
        var plugins = EchoPluginHost.PluginHost(host);

        var status = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
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

        // 清单声明了 capability:kernel.pipeline 却没有授予：发现阶段就被拒绝、不启动进程，插件以 Failed 上报（lifecycle.md §1）
        await using var denied = await EchoPluginHost.StartAsync(grantPipeline: false);
        var status = EchoPluginHost.PluginHost(denied).FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
        Assert.Equal(LifecycleState.Failed, status.Status.State);
        Assert.Equal(PluginErrors.PermissionDenied, status.Status.Error?.Code);
        Assert.Null(status.ProcessId);
        Assert.Equal(LifecycleState.Degraded, denied.App.State);
        Assert.Equal(LifecycleState.Failed, Assert.Single(denied.App.Health().Contributions, static c => c.Name == "plugins." + EchoPluginHost.PluginId).State);
        Assert.False(denied.Services.GetRequiredService<OperationRegistry>().Find("echoes.create").HasValue);
    }

    [Fact]
    public async Task Discovery_KeepsRejectedPluginsAsFailed_ReportsHealth_AndLogsThem()
    {
        var unreadable = Directory.CreateTempSubdirectory("xihan-plugin-tests-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(unreadable.FullName, "xihan.manifest.json"), "{ not a manifest");
            using var missingEntry = new PluginCopy(EchoPluginHost.Directory);
            missingEntry.EditManifest(static manifest => manifest.Replace("\"entry\": \"EchoPlugin\"", "\"entry\": \"Missing\"", StringComparison.Ordinal));
            using var logs = new LogCapture();
            await using var host = await TestHost.StartAsync(
                static b => b.UseFeature<OutOfProcessPluginsFeature>(),
                [
                    new("XiHan:Plugins:OutOfProcess:Directories:0", unreadable.FullName),
                    new("XiHan:Plugins:OutOfProcess:Directories:1", missingEntry.Directory),
                    new("XiHan:Plugins:OutOfProcess:Grants:xihan.samples.echo:0", "capability:kernel.pipeline"),
                ],
                services => services.AddSingleton<ILoggerFactory>(new LoggerFactory([logs])));
            var plugins = EchoPluginHost.PluginHost(host);

            // 入口文件不存在：以清单 ID 登记为 Failed，可描述，没有进程
            var rejected = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
            Assert.Equal(LifecycleState.Failed, rejected.Status.State);
            Assert.Equal(PluginErrors.NotFound, rejected.Status.Error?.Code);
            Assert.Null(rejected.ProcessId);
            Assert.Equal(EchoPluginHost.PluginId, Assert.Single(plugins.Plugins).Manifest.Id);

            // 两者都是健康条目，宿主因此 Degraded；清单读不出来的没有清单 ID，条目名取目录名
            Assert.Equal(LifecycleState.Degraded, host.App.State);
            var contributions = host.App.Health().Contributions;
            Assert.Equal(LifecycleState.Failed, Assert.Single(contributions, static c => c.Name == "plugins." + EchoPluginHost.PluginId).State);
            var unreadableEntry = Assert.Single(contributions, c => c.Name == "plugins." + unreadable.Name);
            Assert.Equal(LifecycleState.Failed, unreadableEntry.State);
            Assert.Equal(PluginErrors.InvalidSource, unreadableEntry.Error?.Code);

            // 配置阶段还没有宿主日志，附着时补记为错误
            var errors = logs.Entries.Where(static e => e.Level == LogLevel.Error).Select(static e => e.Message).ToList();
            Assert.Contains(errors, static m => m.Contains(EchoPluginHost.PluginId, StringComparison.Ordinal) && m.Contains(PluginErrors.NotFound, StringComparison.Ordinal));
            Assert.Contains(errors, m => m.Contains(unreadable.FullName, StringComparison.Ordinal) && m.Contains(PluginErrors.InvalidSource, StringComparison.Ordinal));

            // 被拒的插件可以卸载
            Assert.True((await plugins.UnloadAsync(EchoPluginHost.PluginId, CancellationToken.None)).IsSuccess);
            Assert.Empty(plugins.Plugins);
        }
        finally
        {
            unreadable.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Discovery_RejectsAPluginWhoseManifestContributesMiddleware_AndTheHostStillStarts()
    {
        // 只有 Wasm 插件能贡献中间件（RFC-0017「范围」）：清单写了 contributes.pipeline 的进程外插件在发现时被拒，
        // 不再照常加载、把这个字段静默忽略
        using var copy = new PluginCopy(EchoPluginHost.Directory);
        copy.EditManifest(static manifest => manifest.Replace("\"contributes\": {", "\"contributes\": {\n    \"pipeline\": [ { \"middleware\": \"echo-guard\", \"stage\": \"observe\" } ],", StringComparison.Ordinal));
        using var logs = new LogCapture();
        await using var host = await EchoPluginHost.StartAsync(directory: copy.Directory, services: services => services.AddSingleton<ILoggerFactory>(new LoggerFactory([logs])));
        var plugins = EchoPluginHost.PluginHost(host);

        // 宿主照常启动，插件按发现时被拒登记：Failed、可描述、没有进程，代理操作没有登记
        var rejected = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
        Assert.Equal(LifecycleState.Failed, rejected.Status.State);
        Assert.Equal(PluginErrors.InvalidSource, rejected.Status.Error?.Code);
        Assert.Equal(PluginMessages.PipelineUnsupported, rejected.Status.Error?.MessageKey);
        Assert.Equal("echo-guard", rejected.Status.Error?.Details?["middleware"]);
        Assert.Null(rejected.ProcessId);
        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find("echoes.create").HasValue);

        // 是健康条目，宿主因此 Degraded 而不是启动失败；附着时记错误日志
        Assert.Equal(LifecycleState.Degraded, host.App.State);
        var entry = Assert.Single(host.App.Health().Contributions, static c => c.Name == "plugins." + EchoPluginHost.PluginId);
        Assert.Equal(LifecycleState.Failed, entry.State);
        Assert.Equal(PluginErrors.InvalidSource, entry.Error?.Code);
        Assert.Contains(logs.Entries, static e => e.Level == LogLevel.Error && e.Message.Contains(EchoPluginHost.PluginId, StringComparison.Ordinal) && e.Message.Contains(PluginErrors.InvalidSource, StringComparison.Ordinal));

        // 运行期显式加载同一个插件：同一错误直接返回给调用方，不登记
        Assert.True((await plugins.UnloadAsync(EchoPluginHost.PluginId, CancellationToken.None)).IsSuccess);
        var loaded = await plugins.LoadAsync(new PluginSource(copy.Directory, new PermissionGrant(["capability:kernel.pipeline"])), CancellationToken.None);
        Assert.Equal(PluginErrors.InvalidSource, loaded.Error?.Code);
        Assert.Equal(PluginMessages.PipelineUnsupported, loaded.Error?.MessageKey);
        Assert.Empty(plugins.Plugins);
    }

    [Fact]
    public async Task DiscoveredPlugin_DeclaredPermissionCodes_AreRegisteredByTheHost()
    {
        // 副本清单在 contributes.permissions 里声明两个码：宿主在配置阶段登记代理操作时一并替它登记（ADR-0080）
        using var copy = new PluginCopy(EchoPluginHost.Directory);
        copy.EditManifest(static manifest => manifest.Replace("\"operations\": [ \"echoes.create\"", "\"permissions\": [ \"echoes.audit\", \"echoes.export\" ], \"operations\": [ \"echoes.create\"", StringComparison.Ordinal));
        await using var host = await EchoPluginHost.StartAsync(directory: copy.Directory);
        Assert.Equal(LifecycleState.Ready, EchoPluginHost.PluginHost(host).FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Status.State);

        var permissions = host.Services.GetRequiredService<PermissionRegistry>();
        Assert.Equal("echoes.audit", permissions.Find("echoes.audit").ValueOrThrow().DisplayName);
        Assert.True(permissions.Find("echoes.export").HasValue);
        Assert.True(host.Services.GetRequiredService<OperationRegistry>().Find("echoes.create").HasValue);
    }

    [Fact]
    public async Task DiscoveredPlugin_DeclaringACodeTheHostDefines_FailsTheAssembly()
    {
        using var copy = new PluginCopy(EchoPluginHost.Directory);
        copy.EditManifest(static manifest => manifest.Replace("\"operations\": [ \"echoes.create\"", "\"permissions\": [ \"echoes.audit\" ], \"operations\": [ \"echoes.create\"", StringComparison.Ordinal));

        // 宿主特性已定义 echoes.audit：插件再声明是冲突，配置阶段即启动失败
        var exception = await Assert.ThrowsAsync<FrameworkException>(() => EchoPluginHost.StartAsync(static b => b.UseFeature(new StubFeature(TestManifests.Create("xihan.app.audit"))
        {
            OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("echoes.audit", "Audit echoes")),
        }), directory: copy.Directory));

        Assert.Equal(KernelErrors.DuplicatePermission, exception.Code);
        Assert.Contains("echoes.audit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_IsDenied_WhenThePermissionIsGrantedButNotDeclared()
    {
        // 副本清单不声明任何权限，宿主却授予了 capability:kernel.pipeline：授予多于声明的部分不生效（invocation.md §5）
        using var copy = new PluginCopy(EchoPluginHost.Directory);
        copy.EditManifest(static manifest => manifest.Replace("\"permissions\": [ \"capability:kernel.pipeline\" ]", "\"permissions\": [ ]", StringComparison.Ordinal));
        await using var host = await EchoPluginHost.StartAsync(static b => b.UseFeature(PingFeature()), directory: copy.Directory);
        Assert.Equal(LifecycleState.Ready, EchoPluginHost.PluginHost(host).FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Status.State);

        var relayed = await EchoPluginHost.InvokeAsync(host, "echoes.relay", """{"operation":"pings.create","payload":{"name":"from-plugin"}}""");

        // EchoPlugin 把回调被拒时尾部元数据里的错误码原样作为失败返回
        Assert.Equal(PluginErrors.CallbackDenied, relayed.Outcome.Error?.Code);
    }

    [Fact]
    public async Task HandlerException_IsReportedAsUnexpected_NotAsUnavailable()
    {
        await using var host = await EchoPluginHost.StartAsync();

        // operation 不是字符串：插件的处理器抛出，gRPC 以 Unknown 结束调用，进程仍在运行（invocation.md §3：除 Unavailable 外都是 PLUGINS_UNEXPECTED_001）
        var response = await EchoPluginHost.InvokeAsync(host, "echoes.relay", """{"operation":5}""");

        Assert.Equal(PluginErrors.Unexpected, response.Outcome.Error?.Code);
        Assert.StartsWith("Unknown:", response.Outcome.Error!.Details!["reason"] as string, StringComparison.Ordinal);
        Assert.Equal(LifecycleState.Ready, EchoPluginHost.PluginHost(host).FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Status.State);
    }

    [Fact]
    public async Task Deadline_IsPropagatedToThePlugin()
    {
        // 宿主这边的截止时间按注入的时钟判定（DeadlineMiddleware 与插件运行时都取容器里的 TimeProvider）：时钟不推进，宿主就不会
        // 自己超时，用例不受墙钟快慢影响。时钟从当前时刻起步，截止时间放在一小时后，gRPC 截止时间换算出的 grpc-timeout 也是一小时；
        // 带到 100 纳秒的零头，核对 RFC 3339 往返不丢精度
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await EchoPluginHost.StartAsync(static b => b.UseFeature(PingFeature()), services: s => s.AddSingleton<TimeProvider>(clock));
        var deadline = clock.GetUtcNow().AddHours(1).AddTicks(1_234_567);
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, deadline: deadline);

        // 插件把收到的 InvokeRequest.metadata 原样带进宿主回调 pings.create，宿主按其中的 deadline 建回调上下文（invocation.md §2）：
        // 回显出的截止时间就是插件进程收到的那一个
        var response = await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest("echoes.relay", Encoding.UTF8.GetBytes("""{"operation":"pings.create","payload":{"name":"deadline"}}""")));

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        var received = EchoPluginHost.Payload(response).GetProperty("deadline").GetDateTimeOffset();
        Assert.Equal(deadline.UtcTicks, received.UtcTicks);
    }

    [Theory]
    [InlineData(true, false, KernelErrors.DeadlineExceeded)]
    [InlineData(false, true, KernelErrors.Cancelled)]
    [InlineData(true, true, KernelErrors.DeadlineExceeded)]
    public async Task CancellationDuringAPluginCall_IsAttributedByTheDeadlineToken(bool deadlineReached, bool callerCancels, string expected)
    {
        // 插件调用进行中（echoes.relay 回调一直等待的宿主操作）时到截止时间、调用方取消或两者都发生：运行时把 gRPC 的 Cancelled
        // 原样写成取消、不按时钟判断，DeadlineMiddleware 按截止令牌归因——截止时间到了即超时，调用方同时取消也一样（RFC-0004）
        // 时钟从当前时刻起步、截止时间在一小时后：gRPC 按墙钟换算的截止时间不会先到，超时只来自宿主按注入时钟落下的定时取消
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var probe = new CancellationProbe();
        await using var host = await EchoPluginHost.StartAsync(b => b.UseFeature(new StubFeature(TestManifests.Create("xihan.app.waits"))
        {
            OnConfigure = ctx =>
            {
                ctx.Services.AddSingleton(probe);
                ctx.AddOperation<WaitOperation, PingRequest, PingResponse>(new OperationContract("waits.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse);
            },
        }), services: s => s.AddSingleton<TimeProvider>(clock));
        using var caller = new CancellationTokenSource();
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, caller.Token, deadline: clock.GetUtcNow().AddHours(1));
        var call = scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest("echoes.relay", Encoding.UTF8.GetBytes("""{"operation":"waits.create","payload":{"name":"wait"}}"""))).AsTask();
        if (await Task.WhenAny(probe.Entered, call).WaitAsync(TimeSpan.FromSeconds(30)) != probe.Entered)
        {
            Assert.Fail($"The plugin call ended before its callback reached the host: {(await call).Outcome}.");
        }

        // 先到截止时间（定时取消在 Advance 里同步触发），再取消调用方的令牌
        if (deadlineReached)
        {
            clock.Advance(TimeSpan.FromHours(1));
        }

        if (callerCancels)
        {
            await caller.CancelAsync();
        }

        var response = await call.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(expected, response.Outcome.Error?.Code);
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
    public async Task HandshakeFailureAtStartup_IsRetriedByTheRestartPolicy_AndTheOperationsAreRegisteredLate()
    {
        // 副本拿掉一个依赖：进程启动即崩溃、写不出握手行（发现阶段的握手失败）
        using var copy = new PluginCopy(EchoPluginHost.Directory);
        var dependency = Path.Combine(copy.Directory, "Grpc.AspNetCore.Server.dll");
        File.Move(dependency, dependency + ".off");
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await EchoPluginHost.StartAsync(directory: copy.Directory, services: s => s.AddSingleton<TimeProvider>(clock));
        var plugins = EchoPluginHost.PluginHost(host);
        var registry = host.Services.GetRequiredService<OperationRegistry>();

        var failed = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
        Assert.Equal(LifecycleState.Failed, failed.Status.State);
        Assert.Equal(PluginErrors.Crashed, failed.Status.Error?.Code);
        Assert.Equal(0, failed.Restarts);
        Assert.Equal(LifecycleState.Degraded, host.App.State);
        Assert.False(registry.Find("echoes.create").HasValue);

        // 依赖放回后，把宿主时钟推过退避（1 秒起），第一次重试就成功：不是一次失败就永远放弃（handshake.md §5）
        File.Move(dependency + ".off", dependency);
        Assert.True(await RecoversAfterBackoffAsync(plugins, clock, TimeSpan.FromSeconds(60)));
        Assert.Equal(1, plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Restarts);
        Assert.Equal(LifecycleState.Ready, host.App.State);

        // 声明到这时才有：代理操作按运行期加载补登记，经内核调用器可用
        Assert.True(registry.Find("echoes.create").HasValue);
        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"late"}""");
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
    }

    [Fact]
    public async Task HandshakeFailureOfARuntimeLoad_IsReturned_AndThenRetried()
    {
        using var copy = new PluginCopy(EchoPluginHost.Directory);
        var dependency = Path.Combine(copy.Directory, "Grpc.AspNetCore.Server.dll");
        File.Move(dependency, dependency + ".off");
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<OutOfProcessPluginsFeature>(), services: s => s.AddSingleton<TimeProvider>(clock));
        var plugins = host.Services.GetRequiredService<IPluginHost>();

        var loaded = await plugins.LoadAsync(new PluginSource(copy.Directory, new PermissionGrant(["capability:kernel.pipeline"])), CancellationToken.None);

        // 失败返回给调用方，插件保持 Failed 并按重启策略重试（lifecycle.md §6）
        Assert.Equal(PluginErrors.Crashed, loaded.Error?.Code);
        Assert.Equal(LifecycleState.Failed, Assert.Single(plugins.Plugins).State);
        File.Move(dependency + ".off", dependency);
        var diagnostics = EchoPluginHost.PluginHost(host);
        Assert.True(await RecoversAfterBackoffAsync(diagnostics, clock, TimeSpan.FromSeconds(60)));
        Assert.Equal(LifecycleState.Ready, Assert.Single(plugins.Plugins).State);

        // 退避只过了一次：依赖放回后的第一次重试就成功
        Assert.Equal(1, diagnostics.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Restarts);
        var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"late"}""");
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
    }

    [Fact]
    public async Task Crash_MarksFailed_DegradesTheApp_AndRestartsWithBackoff()
    {
        await using var host = await EchoPluginHost.StartAsync();
        var plugins = EchoPluginHost.PluginHost(host);
        var firstProcess = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().ProcessId;

        var crashed = await EchoPluginHost.InvokeAsync(host, "echoes.crash", "{}");
        Assert.True(crashed.Outcome.IsFailure);

        Assert.True(await EchoPluginHost.WaitUntilAsync(() => plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Status.State == LifecycleState.Failed, TimeSpan.FromSeconds(10)));
        var failed = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
        Assert.Equal(PluginErrors.Crashed, failed.Status.Error?.Code);
        Assert.Equal(LifecycleState.Degraded, host.App.State);
        Assert.True(host.App.Health().Ready);

        // 退避 1 秒后重启：新进程、累计重启次数 1、应用回到 Ready
        Assert.True(await EchoPluginHost.WaitUntilAsync(() => plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Status.State == LifecycleState.Ready, TimeSpan.FromSeconds(20)));
        var restarted = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow();
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
        var processId = EchoPluginHost.PluginHost(host).FindStatus(EchoPluginHost.PluginId).ValueOrThrow().ProcessId!.Value;
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
        // 卸载撤回了代理操作：按 ID 调用即操作未登记，不进管道（ADR-0085）
        Assert.Equal(KernelErrors.UnknownOperation, (await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"gone"}""")).Outcome.Error?.Code);

        // 直接再加载，不用重启宿主
        Assert.Equal(LifecycleState.Ready, (await plugins.LoadAsync(source, CancellationToken.None)).ValueOrThrow().State);
        var again = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"again"}""");
        Assert.True(again.Outcome.IsSuccess, again.Outcome.ToString());
        Assert.Equal("again", EchoPluginHost.Payload(again).GetProperty("text").GetString());
    }

    [Fact]
    public async Task Unload_WithdrawsTheProxyOperationsAndDeclaredCodes_AndADiscoveredPluginLoadsAgain()
    {
        // 配置阶段发现的插件：副本清单声明一个权限码，宿主连同代理操作一起登记（ADR-0080）
        using var copy = new PluginCopy(EchoPluginHost.Directory);
        copy.EditManifest(static manifest => manifest.Replace("\"operations\": [ \"echoes.create\"", "\"permissions\": [ \"echoes.audit\" ], \"operations\": [ \"echoes.create\"", StringComparison.Ordinal));
        await using var host = await EchoPluginHost.StartAsync(directory: copy.Directory);
        var plugins = EchoPluginHost.PluginHost(host);
        var operations = host.Services.GetRequiredService<OperationRegistry>();
        var permissions = host.Services.GetRequiredService<PermissionRegistry>();
        Assert.Equal(4, operations.All().Count(static d => d.OwnerId == EchoPluginHost.PluginId));

        Assert.True((await plugins.UnloadAsync(EchoPluginHost.PluginId, CancellationToken.None)).IsSuccess);

        // 按归属一次撤回：插件的代理操作与它声明的码都不在了，调用即操作未登记
        Assert.DoesNotContain(operations.All(), static d => d.OwnerId == EchoPluginHost.PluginId);
        Assert.False(permissions.Find("echoes.audit").HasValue);
        Assert.Equal(KernelErrors.UnknownOperation, (await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"gone"}""")).Outcome.Error?.Code);

        // 同一插件直接再加载：重新登记，不撞上自己上一次留下的操作与码（KERNEL_OPERATION_001 / KERNEL_SECURITY_001）
        var reloaded = await plugins.LoadAsync(new PluginSource(copy.Directory, new PermissionGrant(["capability:kernel.pipeline"])), CancellationToken.None);
        Assert.Equal(LifecycleState.Ready, reloaded.ValueOrThrow().State);
        Assert.Equal(4, operations.All().Count(static d => d.OwnerId == EchoPluginHost.PluginId));
        Assert.True(permissions.Find("echoes.audit").HasValue);
        var after = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"reloaded"}""");
        Assert.True(after.Outcome.IsSuccess, after.Outcome.ToString());
        Assert.Equal("reloaded", EchoPluginHost.Payload(after).GetProperty("text").GetString());
    }

    [Fact]
    public async Task Unload_WhileCallsAreInFlight_EveryCallEndsWithAKnownOutcome_AndLaterCallsFindNoOperation()
    {
        await using var host = await EchoPluginHost.StartAsync();
        var plugins = EchoPluginHost.PluginHost(host);
        var outcomes = new ConcurrentQueue<string>();
        using var stop = new CancellationTokenSource();

        // 八个调用方不停调用；插件里每次停 20 毫秒，卸载时总有调用正在插件里执行或排在进程前
        var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var response = await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"busy","delayMs":20}""");
                outcomes.Enqueue(response.Outcome.IsSuccess ? "ok" : response.Outcome.Error!.Code);
            }
        })).ToList();
        Assert.True(await EchoPluginHost.WaitUntilAsync(() => outcomes.Contains("ok"), TimeSpan.FromSeconds(10)));

        Assert.True((await plugins.UnloadAsync(EchoPluginHost.PluginId, CancellationToken.None)).IsSuccess);
        var completedBeforeUnload = outcomes.Count;
        // 卸载完成之后发起的调用都找不到操作；等到确实有这样的结果再停下调用方
        Assert.True(await EchoPluginHost.WaitUntilAsync(() => outcomes.Skip(completedBeforeUnload).Count(static o => o == KernelErrors.UnknownOperation) >= 8, TimeSpan.FromSeconds(10)));
        await stop.CancelAsync();
        await Task.WhenAll(callers);

        // 每个调用都正常结束（没有异常）：卸载前完成、撤回前已查到操作而在进程停止后以不可用结束、或撤回后找不到操作
        Assert.All(outcomes, static o => Assert.Contains(o, new[] { "ok", PluginErrors.Unavailable, KernelErrors.UnknownOperation }));
        Assert.Equal(KernelErrors.UnknownOperation, (await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"after"}""")).Outcome.Error?.Code);
        Assert.DoesNotContain(host.Services.GetRequiredService<OperationRegistry>().All(), static d => d.OwnerId == EchoPluginHost.PluginId);

        // 卸载干净：同一插件直接再加载并服务
        Assert.True((await plugins.LoadAsync(new PluginSource(EchoPluginHost.Directory, new PermissionGrant(["capability:kernel.pipeline"])), CancellationToken.None)).IsSuccess);
        Assert.True((await EchoPluginHost.InvokeAsync(host, "echoes.create", """{"text":"back"}""")).Outcome.IsSuccess);
    }

    /// <summary>
    /// 登记宿主侧操作 pings.create 的桩特性
    /// </summary>
    private static StubFeature PingFeature() => new(TestManifests.Create("xihan.app.pings"))
    {
        OnConfigure = static ctx => ctx.AddOperation<PingOperation, PingRequest, PingResponse>(new OperationContract("pings.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
    };

    /// <summary>
    /// 把宿主时钟推过一次重启退避，再按真实时间等插件回到 Ready
    /// </summary>
    /// <remarks>
    /// 重启退避与读握手行的时限按同一个宿主时钟计时，而重试拉起插件进程耗的是真实时间：重试开始后还推进，时钟会在进程启动期间走完握手时限，这次重试被记成握手超时、多重启一次。返回插件是否在 <paramref name="timeout"/> 内回到 Ready
    /// </remarks>
    private static async Task<bool> RecoversAfterBackoffAsync(OutOfProcessPluginHost plugins, FakeTimeProvider clock, TimeSpan timeout)
    {
        var restarts = plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Restarts;

        // 重启计数在退避结束后、拉起进程前递增：计数没变就还在退避里，只在这时推进时钟
        var retried = await EchoPluginHost.WaitUntilAsync(() =>
        {
            if (plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Restarts == restarts)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
            }

            return plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Restarts != restarts;
        }, timeout);

        // 重试已经开始：时钟停住，握手时限不会被推过，按真实时间等这次重试的结果
        return retried && await EchoPluginHost.WaitUntilAsync(() => plugins.FindStatus(EchoPluginHost.PluginId).ValueOrThrow().Status.State == LifecycleState.Ready, timeout);
    }

    /// <summary>
    /// 进程是否仍在运行
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

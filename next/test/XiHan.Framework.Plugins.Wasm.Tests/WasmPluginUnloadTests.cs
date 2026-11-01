// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 卸载按归属撤回插件登记的代理操作与权限码，之后可以直接再加载（<c>spec/plugin/invocation.md</c> §1，ADR-0085）。
/// </summary>
public sealed class WasmPluginUnloadTests
{
    [Fact]
    public async Task Unload_WithdrawsTheProxyOperationAndDeclaredCodes_AndThePluginLoadsAgain()
    {
        using var declaring = WatPlugin.Declaring(["wat.audit", "wat.export"], null);
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>());
        var plugins = host.Services.GetRequiredService<IPluginHost>();
        var operations = host.Services.GetRequiredService<OperationRegistry>();
        var permissions = host.Services.GetRequiredService<PermissionRegistry>();
        var source = new PluginSource(declaring.Directory, new PermissionGrant([]));
        Assert.True((await plugins.LoadAsync(source, CancellationToken.None)).IsSuccess);

        Assert.True((await plugins.UnloadAsync(WatPlugin.PluginId, CancellationToken.None)).IsSuccess);

        // 代理操作与清单声明的码一次撤回；按 ID 调用即操作未登记，不进管道
        Assert.False(operations.Find(WatPlugin.Operation).HasValue);
        Assert.False(permissions.Find("wat.audit").HasValue);
        Assert.False(permissions.Find("wat.export").HasValue);
        Assert.Equal(KernelErrors.UnknownOperation, (await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}")).Outcome.Error?.Code);

        // 同一插件直接再加载：不撞上自己上一次留下的操作与码
        var reloaded = await plugins.LoadAsync(source, CancellationToken.None);
        Assert.True(reloaded.IsSuccess, reloaded.ToString());
        Assert.True(operations.Find(WatPlugin.Operation).HasValue);
        Assert.True(permissions.Find("wat.audit").HasValue);
    }

    [Fact]
    public async Task Unload_WhileCallsAreInFlight_EveryCallEndsWithAKnownOutcome_AndLaterCallsFindNoOperation()
    {
        // 插件的操作经 xihan.invoke 回调宿主的 pings.create：每次调用都在实例里跑一段，卸载时总有调用在执行或排队
        using var plugin = WatPlugin.Invoking("pings.create", [WasmHostFunctions.PipelinePermission]);
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>().UseFeature(PingFeature()));
        var plugins = host.Services.GetRequiredService<IPluginHost>();
        var source = new PluginSource(plugin.Directory, new PermissionGrant([WasmHostFunctions.PipelinePermission]));
        Assert.True((await plugins.LoadAsync(source, CancellationToken.None)).IsSuccess);
        var outcomes = new ConcurrentQueue<string>();
        using var stop = new CancellationTokenSource();

        var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                var response = await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}");
                outcomes.Enqueue(response.Outcome.IsSuccess ? "ok" : response.Outcome.Error!.Code);
            }
        })).ToList();
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => outcomes.Contains("ok"), TimeSpan.FromSeconds(10)));

        Assert.True((await plugins.UnloadAsync(WatPlugin.PluginId, CancellationToken.None)).IsSuccess);
        var completedBeforeUnload = outcomes.Count;
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => outcomes.Skip(completedBeforeUnload).Count(static o => o == KernelErrors.UnknownOperation) >= 8, TimeSpan.FromSeconds(10)));
        await stop.CancelAsync();
        await Task.WhenAll(callers);

        // 每个调用都正常结束：卸载前完成、撤回前已查到操作而在实例丢弃后以不可用结束、或撤回后找不到操作
        Assert.All(outcomes, static o => Assert.Contains(o, new[] { "ok", PluginErrors.Unavailable, KernelErrors.UnknownOperation }));
        Assert.Equal(KernelErrors.UnknownOperation, (await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}")).Outcome.Error?.Code);

        // 卸载干净：同一插件直接再加载并服务
        Assert.True((await plugins.LoadAsync(source, CancellationToken.None)).IsSuccess);
        var again = await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}");
        Assert.True(again.Outcome.IsSuccess, again.Outcome.ToString());
    }

    [Fact]
    public async Task Unload_OfAPluginContributingMiddleware_LeavesTheMiddlewareFailingClosed()
    {
        // 中间件在管道构建前登记，不随卸载撤回：卸载后经过它所在阶段的调用失败即关闭（wasm.md §6），换版本要重启宿主
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature(PingFeature()));
        var plugins = host.Services.GetRequiredService<IPluginHost>();
        Assert.True((await EchoWasmPluginHost.InvokeAsync(host, "pings.create", """{"name":"before"}""")).Outcome.IsSuccess);

        Assert.True((await plugins.UnloadAsync("xihan.samples.echowasm", CancellationToken.None)).IsSuccess);

        Assert.False(host.Services.GetRequiredService<OperationRegistry>().Find("echoes.create").HasValue);
        Assert.Equal(PluginErrors.Unavailable, (await EchoWasmPluginHost.InvokeAsync(host, "pings.create", """{"name":"after"}""")).Outcome.Error?.Code);
        Assert.Equal(PluginErrors.InvalidModule, (await plugins.LoadAsync(new PluginSource(EchoWasmPluginHost.Directory, new PermissionGrant(["capability:kernel.pipeline"])), CancellationToken.None)).Error?.Code);
    }

    /// <summary>
    /// 登记宿主侧操作 <c>pings.create</c> 的桩特性。
    /// </summary>
    private static StubFeature PingFeature() => new(TestManifests.Create("xihan.app.pings"))
    {
        OnConfigure = static ctx => ctx.AddOperation<PingOperation, PingRequest, PingResponse>(new OperationContract("pings.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
    };
}

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
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 卸载按归属撤回插件的登记，之后可以直接再加载
/// </summary>
/// <remarks>
/// 撤回的是代理操作、清单声明的权限码（<c>spec/plugin/invocation.md</c> §1，ADR-0085）与贡献的中间件（<c>spec/plugin/wasm.md</c> §6，ADR-0088）
/// </remarks>
public sealed class WasmPluginUnloadTests
{
    /// <summary>
    /// 示例插件的中间件在打印的管道里的写法
    /// </summary>
    private const string Guard = "WasmMiddleware[xihan.samples.echowasm:echo-guard]";

    /// <summary>
    /// 示例插件的中间件拦下租户 <c>blocked</c> 时的错误码
    /// </summary>
    private const string Denied = "ECHOWASM_DENIED_001";

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
    public async Task Unload_WithdrawsTheMiddleware_LaterCallsPassItsStage_AndReloadingBringsItBack()
    {
        // 示例插件的 echo-guard（observe 阶段）把租户 blocked 短路为 ECHOWASM_DENIED_001：用宿主侧操作看它在不在管道里
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature(PingFeature()));
        var plugins = host.Services.GetRequiredService<IPluginHost>();
        var pipeline = host.App.DescribePipeline();
        Assert.Contains(Guard, pipeline, StringComparison.Ordinal);
        Assert.Equal(Denied, (await PingAsync(host, "blocked")).Error?.Code);

        Assert.True((await plugins.UnloadAsync(EchoWasmPluginHost.PluginId, CancellationToken.None)).IsSuccess);

        // 中间件随卸载按归属撤出管道：之后经过 observe 阶段的调用不再失败，插件的操作未登记
        Assert.DoesNotContain(Guard, host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.True((await PingAsync(host, "blocked")).IsSuccess);
        Assert.True((await PingAsync(host, "acme")).IsSuccess);
        Assert.Equal(KernelErrors.UnknownOperation, (await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":"x"}""")).Outcome.Error?.Code);

        // 再加载：中间件回到原来的位置，又开始拦截
        var reloaded = await plugins.LoadAsync(EchoSource, CancellationToken.None);
        Assert.True(reloaded.IsSuccess, reloaded.ToString());
        Assert.Equal(pipeline, host.App.DescribePipeline());
        Assert.Equal(Denied, (await PingAsync(host, "blocked")).Error?.Code);
        Assert.True((await PingAsync(host, "acme")).IsSuccess);
        Assert.True((await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":"x"}""")).Outcome.IsSuccess);
    }

    [Fact]
    public async Task Unload_WhileCallsPassItsMiddleware_CallsStartedEarlierEndKnown_AndCallsStartedAfterwardsAllPass()
    {
        // 中间件时限放宽：八个调用方排队等同一个实例，不让排队把钩子挤到超时、插件按崩溃重建
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature(PingFeature()), ("XiHan:Plugins:Wasm:MiddlewareTimeoutMilliseconds", "10000"));
        var plugins = host.Services.GetRequiredService<IPluginHost>();
        var earlier = new ConcurrentQueue<string>();
        var later = new ConcurrentQueue<string>();
        var unloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();

        var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                // 先看卸载是否已经完成再开始调用：完成之后才进入管道的调用取到的管道里没有插件的中间件
                var afterUnload = unloaded.Task.IsCompleted;
                var outcome = await PingAsync(host, "acme");
                (afterUnload ? later : earlier).Enqueue(outcome.IsSuccess ? "ok" : outcome.Error!.Code);
            }
        })).ToList();
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => earlier.Contains("ok"), TimeSpan.FromSeconds(10)));

        Assert.True((await plugins.UnloadAsync(EchoWasmPluginHost.PluginId, CancellationToken.None)).IsSuccess);
        unloaded.SetResult();
        Assert.True(await EchoWasmPluginHost.WaitUntilAsync(() => later.Count >= 16, TimeSpan.FromSeconds(10)));
        await stop.CancelAsync();
        await Task.WhenAll(callers);

        // 卸载完成之前开始的调用可能取到了带插件中间件的管道：照常完成，或在插件停止后失败即关闭
        Assert.All(earlier, static o => Assert.Contains(o, new[] { "ok", PluginErrors.Unavailable }));
        // 卸载完成之后开始的调用一个都不失败
        Assert.All(later, static o => Assert.Equal("ok", o));
        Assert.DoesNotContain(Guard, host.App.DescribePipeline(), StringComparison.Ordinal);

        // 卸载干净：同一插件直接再加载，中间件回来
        Assert.True((await plugins.LoadAsync(EchoSource, CancellationToken.None)).IsSuccess);
        Assert.Equal(Denied, (await PingAsync(host, "blocked")).Error?.Code);
    }

    [Fact]
    public async Task Unload_OfMiddlewareThatAnotherMiddlewareIsOrderedAgainst_IsRejectedAndChangesNothing()
    {
        // 宿主中间件声明排在插件的 echo-guard 之后：撤回 echo-guard 会留下指向不存在的中间件的约束
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature(PingFeature()).UseFeature(new StubFeature(TestManifests.Create("xihan.app.marker"))
        {
            OnConfigure = static ctx => ctx.Pipeline.UseInstance(new MarkerMiddleware(), PipelineStage.Observe, owner: "xihan.app.marker").After(typeof(MarkerMiddleware).FullName!, $"{EchoWasmPluginHost.PluginId}:echo-guard"),
        }));
        var plugins = host.Services.GetRequiredService<IPluginHost>();
        var pipeline = host.App.DescribePipeline();

        var unloaded = await plugins.UnloadAsync(EchoWasmPluginHost.PluginId, CancellationToken.None);

        // 什么都不撤：插件仍在宿主里，中间件与操作照常
        Assert.Equal(KernelErrors.PipelineRegistration, unloaded.Error?.Code);
        Assert.Equal(pipeline, host.App.DescribePipeline());
        Assert.Equal(LifecycleState.Ready, EchoWasmPluginHost.PluginHost(host).Describe(EchoWasmPluginHost.PluginId)?.Status.State);
        Assert.Equal(Denied, (await PingAsync(host, "blocked")).Error?.Code);
        Assert.True((await EchoWasmPluginHost.InvokeAsync(host, "echoes.create", """{"text":"x"}""")).Outcome.IsSuccess);
    }

    /// <summary>
    /// 运行期加载示例插件的来源（授予回调权限）
    /// </summary>
    private static PluginSource EchoSource => new(EchoWasmPluginHost.Directory, new PermissionGrant([WasmHostFunctions.PipelinePermission]));

    /// <summary>
    /// 以给定租户经完整管道调用宿主侧操作 <c>pings.create</c>
    /// </summary>
    private static async Task<Result> PingAsync(TestHost host, string tenant)
        => (await EchoWasmPluginHost.InvokeAsync(host, "pings.create", """{"name":"ping"}""", new Dictionary<string, string> { [BaggageKeys.Tenant] = tenant })).Outcome;

    /// <summary>
    /// 登记宿主侧操作 <c>pings.create</c> 的桩特性
    /// </summary>
    private static StubFeature PingFeature() => new(TestManifests.Create("xihan.app.pings"))
    {
        OnConfigure = static ctx => ctx.AddOperation<PingOperation, PingRequest, PingResponse>(new OperationContract("pings.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
    };
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 宿主导入 <c>xihan.invoke</c> / <c>xihan.publish</c> 的权限（<c>spec/plugin/wasm.md</c> §4）。
/// </summary>
/// <remarks>
/// 以现场生成的 WAT 插件驱动：清单声明的权限与宿主授予分别可控。
/// </remarks>
public sealed class WasmCallbackTests
{
    [Fact]
    public async Task Invoke_IsDenied_WhenThePermissionIsGrantedButNotDeclared()
    {
        using var undeclared = WatPlugin.Invoking("pings.create", []);
        await using (var host = await StartAsync(undeclared, [WasmHostFunctions.PipelinePermission]))
        {
            Assert.Equal(LifecycleState.Ready, EchoWasmPluginHost.PluginHost(host).Describe(WatPlugin.PluginId)!.Status.State);

            var response = await CallAsync(host);

            Assert.Equal(PluginErrors.CallbackDenied, response.Outcome.Error?.Code);
        }

        // 对照：声明且授予时回调以插件身份执行
        using var declared = WatPlugin.Invoking("pings.create", [WasmHostFunctions.PipelinePermission]);
        await using (var host = await StartAsync(declared, [WasmHostFunctions.PipelinePermission]))
        {
            var response = await CallAsync(host);

            Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
            Assert.Equal(WatPlugin.PluginId, EchoWasmPluginHost.Payload(response).GetProperty("principal").GetString());
        }
    }

    [Fact]
    public async Task Publish_IsDenied_WhenThePermissionIsGrantedButNotDeclared()
    {
        using var undeclared = WatPlugin.Publishing("wat.events", []);
        var denied = new ProbePublisher();
        await using (var host = await StartAsync(undeclared, [WasmHostFunctions.MessagingPermission], services: s => s.AddSingleton<IMessagePublisher>(denied)))
        {
            var response = await CallAsync(host);

            // 插件丢弃 xihan.publish 的应答、回成功；发布是否发生看发布器（回调在导入函数返回前同步完成）
            Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
            Assert.Equal(0, denied.Published);
        }

        using var declared = WatPlugin.Publishing("wat.events", [WasmHostFunctions.MessagingPermission]);
        var allowed = new ProbePublisher();
        await using (var host = await StartAsync(declared, [WasmHostFunctions.MessagingPermission], services: s => s.AddSingleton<IMessagePublisher>(allowed)))
        {
            var response = await CallAsync(host);

            Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
            Assert.Equal(1, allowed.Published);
        }
    }

    [Fact]
    public async Task Invoke_IsBoundByTheOuterDeadline_WhenTheRequestCarriesNone()
    {
        // 插件经 xihan.invoke 调用一直等待的宿主操作，请求里没有截止时间：新调用要随外层调用在截止时间被取消（wasm.md §4）
        var probe = new CancellationProbe();
        using var plugin = WatPlugin.Invoking("waits.create", [WasmHostFunctions.PipelinePermission]);
        await using var host = await StartAsync(plugin, [WasmHostFunctions.PipelinePermission], ctx =>
        {
            ctx.Services.AddSingleton(probe);
            ctx.AddOperation<WaitOperation, PingRequest, PingResponse>(new OperationContract("waits.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse);
        });

        var response = await CallAsync(host, DateTimeOffset.UtcNow.AddMilliseconds(300));

        Assert.Equal(KernelErrors.DeadlineExceeded, response.Outcome.Error?.Code);
        Assert.True(await probe.Observed.WaitAsync(TimeSpan.FromSeconds(30)), "the host operation behind xihan.invoke must be cancelled at the outer deadline");
    }

    [Fact]
    public async Task Publish_IsBoundByTheOuterDeadline()
    {
        var probe = new CancellationProbe();
        var publisher = new ProbePublisher(probe);
        using var plugin = WatPlugin.Publishing("wat.events", [WasmHostFunctions.MessagingPermission]);
        await using var host = await StartAsync(plugin, [WasmHostFunctions.MessagingPermission], services: s => s.AddSingleton<IMessagePublisher>(publisher));

        await CallAsync(host, DateTimeOffset.UtcNow.AddMilliseconds(300));

        Assert.Equal(1, publisher.Published);
        Assert.True(await probe.Observed.WaitAsync(TimeSpan.FromSeconds(30)), "xihan.publish must be cancelled at the outer deadline");
    }

    /// <summary>
    /// 只装配 Wasm 插件特性与宿主侧 pings.create 的宿主。
    /// </summary>
    private static Task<TestHost> StartAsync(WatPlugin plugin, IReadOnlyList<string> grants, Action<FeatureContext>? configure = null, Action<IServiceCollection>? services = null)
    {
        var configuration = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory),
            new("XiHan:Plugins:Wasm:EpochIntervalMilliseconds", "5"),
        };
        configuration.AddRange(grants.Select(static (grant, index) => new KeyValuePair<string, string?>($"XiHan:Plugins:Wasm:Grants:{WatPlugin.PluginId}:{index}", grant)));
        return TestHost.StartAsync(
            b => b.UseFeature<WasmPluginsFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.pings"))
            {
                OnConfigure = ctx =>
                {
                    ctx.AddOperation<PingOperation, PingRequest, PingResponse>(new OperationContract("pings.create"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse);
                    configure?.Invoke(ctx);
                },
            }),
            configuration,
            services);
    }

    /// <summary>
    /// 调用插件唯一的操作。
    /// </summary>
    private static async Task<OperationResponse> CallAsync(TestHost host, DateTimeOffset? deadline = null)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, deadline: deadline);
        return await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest(WatPlugin.Operation, Encoding.UTF8.GetBytes("{}")));
    }
}

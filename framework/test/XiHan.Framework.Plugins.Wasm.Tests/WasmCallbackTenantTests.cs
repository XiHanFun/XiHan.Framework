// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 插件的回调、发布与中间件不能自己挑租户（ADR-0195）
/// </summary>
/// <remarks>
/// 安全审查 S-30：原先回调元数据里的 <c>xihan.tenant</c> 原样交给 Tenant 阶段，插件身份没有租户声明可比对，中间件也能改写它；插件能在任何启用的租户里调用宿主操作、发布消息
/// </remarks>
public sealed class WasmCallbackTenantTests
{
    [Fact]
    public async Task Invoke_NamingAnotherTenant_IsDenied()
    {
        using var plugin = WatPlugin.InvokingWithBaggage(EchoTenant, [WasmHostFunctions.PipelinePermission], Tenant("globex"));
        await using var host = await StartAsync(plugin, [WasmHostFunctions.PipelinePermission]);

        var response = await CallAsync(host, WatPlugin.Operation, Tenant("acme"));

        // 修之前回调在 globex 里执行：外层调用属于 acme，插件自报的 globex 照样通过 Tenant 阶段
        Assert.Equal(PluginErrors.CallbackDenied, response.Outcome.Error?.Code);
    }

    [Fact]
    public async Task Invoke_NamingTheTenantOfTheCall_RunsInIt()
    {
        using var plugin = WatPlugin.InvokingWithBaggage(EchoTenant, [WasmHostFunctions.PipelinePermission], Tenant("acme"));
        await using var host = await StartAsync(plugin, [WasmHostFunctions.PipelinePermission]);

        var response = await CallAsync(host, WatPlugin.Operation, Tenant("acme"));

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.Equal("acme", EchoWasmPluginHost.Payload(response).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Publish_OfAnEnvelopeNamingAnotherTenant_IsDenied()
    {
        using var plugin = WatPlugin.PublishingAs("wat.events", [WasmHostFunctions.MessagingPermission], "globex");
        var publisher = new ProbePublisher();
        await using var host = await StartAsync(plugin, [WasmHostFunctions.MessagingPermission], s => s.AddSingleton<IMessagePublisher>(publisher));

        var response = await CallAsync(host, WatPlugin.Operation, Tenant("acme"));

        // 插件丢弃 xihan.publish 的应答、回成功；修之前信封带着插件自报的 globex 交给发布器，订阅方在 globex 里处理
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.Equal(0, publisher.Published);
    }

    [Fact]
    public async Task Middleware_CannotRewriteTheTenant()
    {
        using var plugin = WatPlugin.WithMiddleware(new JsonArray(new JsonObject { ["middleware"] = "rewrite", ["stage"] = "observe" }), Tenant("globex"));
        await using var host = await StartAsync(plugin, []);

        var response = await CallAsync(host, EchoTenant, Tenant("acme"));

        // 修之前 Observe 阶段的插件中间件把 xihan.tenant 改成 globex，Tenant 阶段随后按它解析
        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.Equal("acme", EchoWasmPluginHost.Payload(response).GetProperty("name").GetString());
    }

    /// <summary>
    /// 宿主侧回显租户的操作 ID
    /// </summary>
    private const string EchoTenant = "tenants.echo";

    /// <summary>
    /// 只带租户的 Baggage
    /// </summary>
    private static Dictionary<string, string> Tenant(string tenant) => new(StringComparer.Ordinal) { [BaggageKeys.Tenant] = tenant };

    /// <summary>
    /// 装配 Wasm 插件、内存多租户与宿主侧 tenants.echo 的宿主
    /// </summary>
    /// <remarks>
    /// acme 与 globex 两个租户都启用
    /// </remarks>
    private static Task<TestHost> StartAsync(WatPlugin plugin, IReadOnlyList<string> grants, Action<IServiceCollection>? services = null)
    {
        var configuration = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory),
            new("XiHan:Plugins:Wasm:EpochIntervalMilliseconds", "5"),
            new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme"),
            new("XiHan:MultiTenancy:Tenants:globex:DisplayName", "Globex"),
        };
        configuration.AddRange(grants.Select(static (grant, index) => new KeyValuePair<string, string?>($"XiHan:Plugins:Wasm:Grants:{WatPlugin.PluginId}:{index}", grant)));
        return TestHost.StartAsync(
            static b => b.UseFeature<WasmPluginsFeature>().UseFeature<InMemoryMultiTenancyFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.tenants"))
            {
                OnConfigure = static ctx => ctx.AddOperation<TenantOperation, PingRequest, PingResponse>(new OperationContract(EchoTenant), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
            }),
            configuration,
            services);
    }

    /// <summary>
    /// 带着给定 Baggage 调用一个操作
    /// </summary>
    private static async Task<OperationResponse> CallAsync(TestHost host, string operation, IReadOnlyDictionary<string, string> baggage)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, new InvocationContextOptions { Baggage = baggage }, CancellationToken.None);
        return await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest(operation, Encoding.UTF8.GetBytes("""{"name":"x"}""")));
    }
}

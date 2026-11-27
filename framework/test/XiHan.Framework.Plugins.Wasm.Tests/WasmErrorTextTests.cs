// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 宿主写给 Wasm 插件的错误文案里的孤立代理项换成 U+FFFD
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §3「写出 <c>Error</c> 的文案」，ADR-0339。宿主侧的操作以文案里有孤立代理项的自由文本错误失败：经回调的应答交给插件、经中间件 <c>after</c> 阶段的 <c>outcome</c> 交给插件。修之前 Wasm 载体的 JSON 写出器遇到孤立的代理项即抛出：回调的应答写不出、插件随之陷阱，<c>after</c> 阶段的请求写不出、整个调用以未处理的异常结束
/// </remarks>
public sealed class WasmErrorTextTests
{
    /// <summary>
    /// 宿主侧失败的操作 ID
    /// </summary>
    private const string FailingOperation = "orders.fail";

    [Fact]
    public async Task CallbackFailure_WithALoneSurrogateInItsText_ReachesThePlugin_WithReplacementCharacters()
    {
        // 插件把宿主的 InvokeResponse 原样作为自己的应答：调用方读到的就是宿主写给插件的那个错误
        using var plugin = WatPlugin.Invoking(FailingOperation, [WasmHostFunctions.PipelinePermission]);
        await using var host = await StartAsync(plugin, [WasmHostFunctions.PipelinePermission]);

        var response = await EchoWasmPluginHost.InvokeAsync(host, WatPlugin.Operation, "{}");

        var error = response.Outcome.Error;
        Assert.NotNull(error);
        Assert.Equal((LiteralFailureOperation.Code, LiteralFailureOperation.ReplacedText), (error.Code, error.Text));
        Assert.Equal("wat", error.Details?["order"]);
        Assert.Equal((LiteralFailureOperation.InnerCode, LiteralFailureOperation.ReplacedText), (error.Inner?.Code, error.Inner?.Text));
        // 回调照常应答，插件没有陷阱
        Assert.Equal((LifecycleState.Ready, 0), (EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status.State, EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Restarts));
    }

    [Fact]
    public async Task MiddlewareOutcome_WithALoneSurrogateInItsText_IsWrittenToThePlugin_AndTheCallKeepsItsFailure()
    {
        // after 阶段把下游的结果写给插件：文案换成 U+FFFD 写出，调用方拿到的仍是宿主操作自己的失败
        using var plugin = WatPlugin.WithMiddleware(new JsonArray(new JsonObject { ["middleware"] = "observer", ["stage"] = "observe" }));
        await using var host = await StartAsync(plugin, []);

        var response = await EchoWasmPluginHost.InvokeAsync(host, FailingOperation, """{"name":"o-1"}""");

        var error = response.Outcome.Error;
        Assert.NotNull(error);
        Assert.Equal((LiteralFailureOperation.Code, LiteralFailureOperation.Text), (error.Code, error.Text));
        Assert.Equal(LifecycleState.Ready, EchoWasmPluginHost.PluginHost(host).FindStatus(WatPlugin.PluginId).ValueOrThrow().Status.State);
    }

    /// <summary>
    /// 装配 Wasm 插件特性与宿主侧失败操作的宿主
    /// </summary>
    private static Task<TestHost> StartAsync(WatPlugin plugin, IReadOnlyList<string> grants)
    {
        var configuration = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory),
            new("XiHan:Plugins:Wasm:EpochIntervalMilliseconds", "5"),
        };
        configuration.AddRange(grants.Select(static (grant, index) => new KeyValuePair<string, string?>($"XiHan:Plugins:Wasm:Grants:{WatPlugin.PluginId}:{index}", grant)));
        return TestHost.StartAsync(
            static b => b.UseFeature<WasmPluginsFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.orders"))
            {
                OnConfigure = static ctx => ctx.AddOperation<LiteralFailureOperation, PingRequest, PingResponse>(new OperationContract(FailingOperation), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
            }),
            configuration);
    }
}

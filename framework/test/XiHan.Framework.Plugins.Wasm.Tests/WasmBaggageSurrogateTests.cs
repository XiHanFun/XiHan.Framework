// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 宿主写给 Wasm 插件的元数据里的 Baggage 值不经宽松编码
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §2（ADR-0334）：上下文里只有幂等键可能带着孤立的代理项。Wasm 载体的 proto3 JSON 写出器遇到它抛出异常，以前调用以 <c>KERNEL_UNEXPECTED_001</c> 结束；中间件的钩子还可能排在 Idempotency 阶段之前。宿主只装配了 Wasm 插件特性，没有 Idempotency 阶段
/// </remarks>
public sealed class WasmBaggageSurrogateTests : IDisposable
{
    /// <summary>
    /// 有孤立代理项的幂等键
    /// </summary>
    private static readonly Dictionary<string, string> _lone = new(StringComparer.Ordinal) { [BaggageKeys.IdempotencyKey] = "k\uD800" };

    /// <summary>
    /// 带成对代理项的幂等键
    /// </summary>
    private static readonly Dictionary<string, string> _paired = new(StringComparer.Ordinal) { [BaggageKeys.IdempotencyKey] = "k😀" };

    /// <summary>
    /// 插件工作目录的根
    /// </summary>
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("xihan-wasm-work-");

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _work.Delete(recursive: true);
        }
        catch (IOException)
        {
            // 留给系统清理临时目录
        }
    }

    [Fact]
    public async Task AnIdempotencyKeyWithALoneSurrogate_FailsTheInvocation_AndTheInstanceKeepsServing()
    {
        using var plugin = WatPlugin.Custom(string.Empty, "{ok}");
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host, _lone);

        var error = response.Outcome.Error;
        Assert.Equal((PluginErrors.Unexpected, PluginMessages.ProcessFailed), (error?.Code, error?.MessageKey));
        Assert.Equal(
            (WatPlugin.PluginId, WatPlugin.Operation, "baggage 'xihan.idempotency-key' has a lone surrogate and cannot be encoded as UTF-8"),
            (error!.Details!["id"], error.Details["phase"], error.Details["reason"]));

        // 成对的代理项照常交给插件；宿主一侧的失败不碰实例，没有重建
        Assert.True((await CallAsync(host, _paired)).Outcome.IsSuccess);
        var status = host.Services.GetRequiredService<WasmPluginHost>().FindStatus(WatPlugin.PluginId).ValueOrThrow();
        Assert.Equal((LifecycleState.Ready, 0), (status.Status.State, status.Restarts));
    }

    [Fact]
    public async Task AnIdempotencyKeyWithALoneSurrogate_FailsAMiddlewareHook_AndTheCallIsClosed()
    {
        // 中间件在 Observe 阶段，排在 Idempotency 阶段之前：钩子是幂等键交给插件的第一处
        using var plugin = WatPlugin.WithMiddleware(new JsonArray(new JsonObject { ["middleware"] = "guard", ["stage"] = "observe" }));
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host, _lone);

        // 失败即关闭：钩子写不出元数据，调用不往下走
        var error = response.Outcome.Error;
        Assert.Equal((PluginErrors.Unexpected, PluginMessages.ProcessFailed), (error?.Code, error?.MessageKey));
        Assert.Equal(
            (WatPlugin.PluginId, "guard", "baggage 'xihan.idempotency-key' has a lone surrogate and cannot be encoded as UTF-8"),
            (error!.Details!["id"], error.Details["phase"], error.Details["reason"]));
        var status = host.Services.GetRequiredService<WasmPluginHost>().FindStatus(WatPlugin.PluginId).ValueOrThrow();
        Assert.Equal((LifecycleState.Ready, 0), (status.Status.State, status.Restarts));
    }

    /// <summary>
    /// 装配只带这个插件的宿主
    /// </summary>
    private Task<TestHost> StartAsync(WatPlugin plugin)
        => TestHost.StartAsync(
            static b => b.UseFeature<WasmPluginsFeature>(),
            [new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory), new("XiHan:Plugins:Wasm:WorkDirectory", _work.FullName)]);

    /// <summary>
    /// 带着这组 Baggage 调用插件唯一的操作
    /// </summary>
    private static async Task<OperationResponse> CallAsync(TestHost host, IReadOnlyDictionary<string, string> baggage)
        => await host.InvokeOperationRawAsync(new OperationRequest(WatPlugin.Operation, "{}"u8.ToArray()), baggage);
}

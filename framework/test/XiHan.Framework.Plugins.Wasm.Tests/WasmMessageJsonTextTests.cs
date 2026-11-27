// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 客体写来的消息过 JSON 文本的共同检查
/// </summary>
/// <remarks>
/// <c>spec/plugin/wasm.md</c> §2 与 RFC-0001「JSON 文本」（ADR-0345）：导出函数的响应（操作应答、中间件钩子的应答）没过检查按崩溃处理，导入函数的请求（回调、发布）以 <c>PLUGINS_VALIDATION_005</c> 应答、实例照常服务，与有非法 UTF-8 的消息同一处置（ADR-0312）。
/// 转义写出的孤立代理项修之前 proto3 JSON 的解析器就拒收（这里钉住）；重复成员与超过 64 层修之前照收：重复的成员后一个覆盖前一个，未知字段里的不看。插件由现场生成的 WAT 写出这样的原文
/// </remarks>
public sealed class WasmMessageJsonTextTests : IDisposable
{
    /// <summary>
    /// 消息在客体内存里的位置：第一页末尾没有数据段的空白
    /// </summary>
    private const int MessageOffset = 60000;

    /// <summary>
    /// 一条合法的信封
    /// </summary>
    private const string Envelope = "{\"topic\":\"wat.events\",\"id\":\"wat-1\",\"source\":\"wat\",\"type\":\"wat.created\",\"data\":\"e30=\"}";

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

    [Theory]
    [InlineData("{\"status\":{\"ok\":false,\"error\":{\"code\":\"ORDERS_VALIDATION_001\",\"text\":\"bad \\ud800\"}}}")]
    [InlineData("{\"status\":{\"ok\":false,\"error\":{\"code\":\"ORDERS_VALIDATION_001\"}},\"status\":{\"ok\":true}}")]
    [InlineData("{\"status\":{\"ok\":true,\"ok\":true}}")]
    public async Task AnOperationResponse_ThatFailsTheCommonCheck_IsNotAValidMessage(string json)
    {
        // 孤立代理项的转义；重复成员修之前后一个覆盖前一个，读成成功
        await AssertNotAValidResponseAsync(json);
    }

    [Fact]
    public async Task AnOperationResponse_NestedDeeperThan64Levels_IsNotAValidMessage()
    {
        // 最外层的对象是第 1 层，未知字段里 64 层数组即第 65 层：修之前未知字段的值不看层数，读成成功
        await AssertNotAValidResponseAsync("{\"status\":{\"ok\":true},\"extra\":" + new string('[', 64) + new string(']', 64) + "}");
    }

    [Theory]
    [InlineData("{\"operation\":\"pings.\\ud800create\",\"payload\":\"e30=\"}")]
    [InlineData("{\"operation\":\"wat.missing\",\"operation\":\"pings.create\",\"payload\":\"e30=\"}")]
    [InlineData("{\"operation\":\"pings.create\",\"payload\":\"e30=\",\"metadata\":{\"baggage\":{\"xihan.correlation-id\":\"a\",\"xihan.correlation-id\":\"b\"}}}")]
    public async Task ACallbackRequest_ThatFailsTheCommonCheck_IsRejectedAsInvalid_AndTheInstanceKeepsServing(string json)
    {
        // 插件没有声明回调的权限：修之前重复的成员照收，请求进到权限那一步，以 PLUGINS_DENIED_002 被拒
        using var plugin = WatPlugin.Custom(string.Empty, $"(call $callback (i32.const {MessageOffset}) (i32.const {json.Length}))", fields: Segment(json));
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.Equal((PluginErrors.InvalidModule, PluginMessages.ModuleInvalid), (response.Outcome.Error?.Code, response.Outcome.Error?.MessageKey));
        Assert.Equal("xihan.invoke received an invalid InvokeRequest", response.Outcome.Error!.Details!["reason"]);
        // 导入函数的请求不合法只让这次回调失败：没有陷阱，实例没有重建
        Assert.Equal((LifecycleState.Ready, 0), Status(host));
    }

    [Theory]
    [InlineData("{\"envelopes\":[" + Envelope + "],\"envelopes\":[" + Envelope + "]}")]
    [InlineData("{\"envelopes\":[{\"topic\":\"wat.events\",\"id\":\"wat-1\",\"source\":\"wat\",\"type\":\"wat.created\",\"data\":\"e30=\",\"extensions\":{\"note\":\"\\udc00\"}}]}")]
    public async Task APublishRequest_ThatFailsTheCommonCheck_PublishesNothing_AndTheInstanceKeepsServing(string json)
    {
        // 插件声明并被授予了发布权限：修之前重复的 envelopes 照收、照常发布
        var publisher = new ProbePublisher();
        var body = $"(drop (call $publish (i32.const {MessageOffset}) (i32.const {json.Length}))) {{ok}}";
        using var plugin = WatPlugin.Custom("(import \"xihan\" \"publish\" (func $publish (param i32 i32) (result i64)))", body, fields: Segment(json), permissions: [WasmHostFunctions.MessagingPermission]);
        await using var host = await StartAsync(plugin, WasmHostFunctions.MessagingPermission, s => s.AddSingleton<IMessagePublisher>(publisher));

        var response = await CallAsync(host);

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.Equal(0, publisher.Published);
        Assert.Equal((LifecycleState.Ready, 0), Status(host));
    }

    [Theory]
    [InlineData("{\"action\":\"MIDDLEWARE_ACTION_CONTINUE\",\"baggage\":{\"xihan.correlation-id\":\"c\\ud800\"}}")]
    [InlineData("{\"action\":\"MIDDLEWARE_ACTION_SHORT_CIRCUIT\",\"action\":\"MIDDLEWARE_ACTION_CONTINUE\"}")]
    public async Task AMiddlewareResponse_ThatFailsTheCommonCheck_ClosesTheCall(string json)
    {
        // 修之前重复的成员后一个覆盖前一个，钩子读成 CONTINUE，调用照常成功
        using var plugin = WatPlugin.WithMiddlewareAnswering(new JsonArray(new JsonObject { ["middleware"] = "guard", ["stage"] = "observe" }), json);
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        // 失败即关闭：before 钩子的应答读不出，不进入下游
        var error = response.Outcome.Error;
        Assert.Equal((PluginErrors.Unexpected, PluginMessages.ProcessFailed), (error?.Code, error?.MessageKey));
        Assert.Equal(("xihan_middleware", "the response is not a valid JSON message"), (error!.Details!["phase"], error.Details["reason"]));
    }

    /// <summary>
    /// 插件的操作原样回这段文本时，调用方得到读不出应答的错误
    /// </summary>
    private async Task AssertNotAValidResponseAsync(string json)
    {
        using var plugin = WatPlugin.Custom(string.Empty, $"(call $pack (i32.const {MessageOffset}) (i32.const {json.Length}))", fields: Segment(json));
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        var error = response.Outcome.Error;
        Assert.Equal((PluginErrors.Unexpected, PluginMessages.ProcessFailed), (error?.Code, error?.MessageKey));
        Assert.Equal(("xihan_invoke", "the response is not a valid JSON message"), (error!.Details!["phase"], error.Details["reason"]));
    }

    /// <summary>
    /// 把 ASCII 的消息原文写成放在 <see cref="MessageOffset"/> 的数据段
    /// </summary>
    private static string Segment(string json)
    {
        var escaped = new StringBuilder(json.Length * 3);
        foreach (var value in Encoding.ASCII.GetBytes(json))
        {
            escaped.Append('\\').Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return $"(data (i32.const {MessageOffset}) \"{escaped}\")";
    }

    /// <summary>
    /// 插件此刻的状态与重启次数
    /// </summary>
    private static (LifecycleState State, int Restarts) Status(TestHost host)
    {
        var status = host.Services.GetRequiredService<WasmPluginHost>().FindStatus(WatPlugin.PluginId).ValueOrThrow();
        return (status.Status.State, status.Restarts);
    }

    /// <summary>
    /// 装配只带这个插件的宿主
    /// </summary>
    /// <param name="plugin">插件</param>
    /// <param name="grant">授予插件的权限；为空时不授予</param>
    /// <param name="services">额外的服务</param>
    private Task<TestHost> StartAsync(WatPlugin plugin, string? grant = null, Action<IServiceCollection>? services = null)
    {
        var configuration = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory),
            new("XiHan:Plugins:Wasm:WorkDirectory", _work.FullName),
        };
        if (grant is not null)
        {
            configuration.Add(new($"XiHan:Plugins:Wasm:Grants:{WatPlugin.PluginId}:0", grant));
        }

        return TestHost.StartAsync(static b => b.UseFeature<WasmPluginsFeature>(), configuration, services);
    }

    /// <summary>
    /// 调用插件唯一的操作
    /// </summary>
    private static async Task<OperationResponse> CallAsync(TestHost host)
        => await host.InvokeOperationRawAsync(new OperationRequest(WatPlugin.Operation, "{}"u8.ToArray()));
}

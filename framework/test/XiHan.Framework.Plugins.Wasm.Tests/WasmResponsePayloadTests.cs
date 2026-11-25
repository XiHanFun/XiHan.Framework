// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 宿主核对 Wasm 插件交回的应答载荷
/// </summary>
/// <remarks>
/// <c>spec/plugin/invocation.md</c> §1 与 RFC-0001「写出与转交」（ADR-0315）：<c>InvokeResponse</c> 本身是合法的 proto3 JSON，<c>payload</c>（base64 的字节）没过「JSON 文本」的共同检查时这次调用是 <c>PLUGINS_UNEXPECTED_001</c>；
/// 消息合法，不按崩溃处理、实例不重建。插件由现场生成的 WAT 写出这样的应答
/// </remarks>
public sealed class WasmResponsePayloadTests : IDisposable
{
    /// <summary>
    /// 应答在客体内存里的位置：第一页末尾没有数据段的空白
    /// </summary>
    private const int MessageOffset = 60000;

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
    [InlineData("{\"a\":1,\"a\":2}", "$.a")]
    [InlineData("{\"a\":\"\\ud800\"}", "$")]
    [InlineData("{\"a\":", "$")]
    public async Task AResponsePayload_ThatIsNotValidJsonText_FailsTheCall_AndTheInstanceKeepsServing(string payload, string path)
    {
        // 修之前载荷原样交回调用方
        using var plugin = Responding(Encoding.UTF8.GetBytes(payload));
        await using var host = await StartAsync(plugin);

        for (var call = 0; call < 2; call++)
        {
            var response = await CallAsync(host);

            Assert.Equal(PluginErrors.Unexpected, response.Outcome.Error?.Code);
            Assert.Equal(PluginMessages.ProcessFailed, response.Outcome.Error!.MessageKey);
            Assert.Equal((WatPlugin.PluginId, WatPlugin.Operation, $"the response payload is not valid JSON text at '{path}'"), (response.Outcome.Error.Details!["id"], response.Outcome.Error.Details["phase"], response.Outcome.Error.Details["reason"]));
            Assert.True(response.Payload.IsEmpty);
        }

        // 不按崩溃处理：实例没有重建，插件一直在服务
        var status = host.Services.GetRequiredService<WasmPluginHost>().FindStatus(WatPlugin.PluginId).ValueOrThrow();
        Assert.Equal((LifecycleState.Ready, 0), (status.Status.State, status.Restarts));
    }

    [Fact]
    public async Task AResponsePayload_WithInvalidUtf8Bytes_FailsTheCall()
    {
        // base64 里是截断的两字节序列：消息本身是合法的 proto3 JSON，载荷的字节不是合法的 UTF-8
        using var plugin = Responding([.. "{\"a\":\""u8, 0xC3, .. "\"}"u8]);
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.Equal(PluginErrors.Unexpected, response.Outcome.Error?.Code);
        Assert.Equal("the response payload is not valid JSON text at '$'", response.Outcome.Error!.Details!["reason"]);
    }

    [Fact]
    public async Task AResponsePayload_ThatIsValidJsonText_IsReturnedUnchanged()
    {
        // 对照：多字节文本照常交回，字节不变
        byte[] payload = [.. "{\"text\":\"订单 😀\"}"u8];
        using var plugin = Responding(payload);
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.Equal(payload, response.Payload.ToArray());
    }

    /// <summary>
    /// 生成一个 <c>xihan_invoke</c> 总是回成功、载荷为给定字节的插件
    /// </summary>
    private static WatPlugin Responding(byte[] payload)
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"status\":{{\"ok\":true}},\"payload\":\"{Convert.ToBase64String(payload)}\"}}");
        var escaped = new StringBuilder(bytes.Length * 3);
        foreach (var value in bytes)
        {
            escaped.Append('\\').Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return WatPlugin.Custom(string.Empty, $"(call $pack (i32.const {MessageOffset}) (i32.const {bytes.Length}))", fields: $"(data (i32.const {MessageOffset}) \"{escaped}\")");
    }

    /// <summary>
    /// 装配只带这个插件的宿主
    /// </summary>
    private Task<TestHost> StartAsync(WatPlugin plugin)
        => TestHost.StartAsync(
            static b => b.UseFeature<WasmPluginsFeature>(),
            [new("XiHan:Plugins:Wasm:Directories:0", plugin.Directory), new("XiHan:Plugins:Wasm:WorkDirectory", _work.FullName)]);

    /// <summary>
    /// 调用插件唯一的操作
    /// </summary>
    private static async Task<OperationResponse> CallAsync(TestHost host)
        => await host.InvokeOperationRawAsync(new OperationRequest(WatPlugin.Operation, "{}"u8.ToArray()));
}

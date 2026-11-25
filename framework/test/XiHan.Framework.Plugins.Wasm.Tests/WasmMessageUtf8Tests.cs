// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 客体写来的消息按严格的 UTF-8 解码
/// </summary>
/// <remarks>
/// <c>spec/plugin/wasm.md</c> §2 与 RFC-0001「JSON 文本」（ADR-0312）：导出函数的响应、导入函数的请求里有非法的 UTF-8 字节序列时，与不是合法的 proto3 JSON 同样处理，不先换成 U+FFFD 再解析。插件由现场生成的 WAT 写出这样的字节
/// </remarks>
public sealed class WasmMessageUtf8Tests : IDisposable
{
    /// <summary>
    /// 消息在客体内存里的位置：第一页末尾没有数据段的空白
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

    [Fact]
    public async Task AResponse_WithAnInvalidUtf8Sequence_IsNotAValidMessage()
    {
        // 错误的文案里有一个截断的两字节序列：修之前宽松解码成 U+FFFD，宿主照常读出插件的错误
        var (data, length) = Message("{\"status\":{\"ok\":false,\"error\":{\"code\":\"ORDERS_VALIDATION_001\",\"text\":\"bad "u8, [0xC3], "\"}}}"u8);
        using var plugin = WatPlugin.Custom(string.Empty, $"(call $pack (i32.const {MessageOffset}) (i32.const {length}))", fields: data);
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.Equal(PluginErrors.Unexpected, response.Outcome.Error!.Code);
        Assert.Equal(PluginMessages.ProcessFailed, response.Outcome.Error.MessageKey);
        Assert.Equal("the response is not a valid JSON message", response.Outcome.Error.Details!["reason"]);
    }

    [Fact]
    public async Task ACallbackRequest_WithAnInvalidUtf8Sequence_IsRejectedAsInvalid()
    {
        // 操作 ID 里有一个编码成 UTF-8 的代理项：修之前解码成 U+FFFD，请求照常进入回调，在权限那一步被拒
        var (data, length) = Message("{\"operation\":\"pings."u8, [0xED, 0xA0, 0x80], "create\",\"payload\":\"e30=\"}"u8);
        using var plugin = WatPlugin.Custom(string.Empty, $"(call $callback (i32.const {MessageOffset}) (i32.const {length}))", fields: data);
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.Equal(PluginErrors.InvalidModule, response.Outcome.Error!.Code);
    }

    [Fact]
    public async Task AResponse_WithMultiByteText_IsReadAsBefore()
    {
        // 对照：合法的多字节序列（含四字节）照常读出
        var (data, length) = Message("{\"status\":{\"ok\":false,\"error\":{\"code\":\"ORDERS_VALIDATION_001\",\"text\":\"订单 😀\"}}}"u8, [], []);
        using var plugin = WatPlugin.Custom(string.Empty, $"(call $pack (i32.const {MessageOffset}) (i32.const {length}))", fields: data);
        await using var host = await StartAsync(plugin);

        var response = await CallAsync(host);

        Assert.Equal("ORDERS_VALIDATION_001", response.Outcome.Error!.Code);
    }

    /// <summary>
    /// 把消息的三段字节拼起来，写成放在 <see cref="MessageOffset"/> 的数据段
    /// </summary>
    /// <returns>WAT 的数据段与消息的字节数</returns>
    private static (string Data, int Length) Message(ReadOnlySpan<byte> head, byte[] middle, ReadOnlySpan<byte> tail)
    {
        byte[] bytes = [.. head, .. middle, .. tail];
        var escaped = new StringBuilder(bytes.Length * 3);
        foreach (var value in bytes)
        {
            escaped.Append('\\').Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return ($"(data (i32.const {MessageOffset}) \"{escaped}\")", bytes.Length);
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

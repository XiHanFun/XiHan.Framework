// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Plugins.OutOfProcess.Processes;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 示例插件在 stdin 关闭时的收尾（<c>lifecycle.md</c> §7.7，ADR-0237）
/// </summary>
/// <remarks>
/// 收尾请求立即取消在途调用，从收到请求起最多 3 秒就以退出码 0 退出，短于宿主发出终止信号后再等的 5 秒
/// </remarks>
public sealed class EchoPluginShutdownTests
{
    [Fact]
    public async Task StdinClosing_DuringACall_CancelsItAndExitsWithinThreeSeconds()
    {
        var token = Guid.NewGuid().ToString("N");
        var executable = OutOfProcessPluginHost.ResolveExecutable(EchoPluginHost.Directory, "EchoPlugin")!;
        var launch = new PluginLaunch(EchoPluginHost.PluginId, executable, EchoPluginHost.Directory, token, [new PluginEndpoint(PluginEndpoint.Tcp, "127.0.0.1:1")], EchoPluginHost.PluginId + "#shutdown", 1, "en-US", TimeSpan.FromSeconds(30));
        await using var process = (await PluginProcess.LaunchAsync(launch, TimeProvider.System, NullLogger.Instance, CancellationToken.None)).ValueOrThrow();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var handshake = new HandshakeRequest { Primitives = 1, InstanceId = "shutdown", DefaultLocale = "en-US" };
        handshake.ProtocolVersions.AddRange(PluginProtocol.SupportedVersions);
        await process.Client.HandshakeAsync(handshake, deadline: deadline);
        Assert.True((await process.Client.ConfigureAsync(new ConfigureRequest { Configuration = "{}" }, deadline: deadline)).Status.Ok);
        Assert.True((await process.Client.StartAsync(new StartRequest(), deadline: deadline)).Status.Ok);
        var call = process.Client.InvokeAsync(new InvokeRequest { Operation = "echoes.create", Payload = ByteString.CopyFromUtf8("""{"text":"slow","delayMs":60000}""") }, deadline: deadline).ResponseAsync;
        await Task.Delay(300);

        var watch = Stopwatch.StartNew();
        process.CloseInput();

        Assert.True(await process.WaitForExitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None), "The plugin must exit within 5 seconds after its stdin is closed.");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4), $"The plugin took {watch.Elapsed} to exit; the wind-down is capped at 3 seconds.");
        Assert.Equal(0, await process.Exited);
        // 在途调用收到了取消，不会一直挂到截止时间
        await Assert.ThrowsAnyAsync<Exception>(() => call.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}

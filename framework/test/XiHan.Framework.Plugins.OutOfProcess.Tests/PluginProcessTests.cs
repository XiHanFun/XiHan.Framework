// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Plugins.OutOfProcess.Processes;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 插件进程的握手等待
/// </summary>
/// <remarks>
/// <c>spec/plugin/handshake.md</c> §1：插件须在握手时限内往 stdout 写出端点行。用读 stdin 却不输出的系统程序作不握手的插件（Windows 上 <c>sort</c>、Unix 上 <c>cat</c>），时限按可推进的时钟计时
/// </remarks>
public sealed class PluginProcessTests
{
    /// <summary>
    /// 握手时限
    /// </summary>
    private static readonly TimeSpan _handshakeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 读 stdin 却不输出的系统程序
    /// </summary>
    private static readonly string _silentExecutable = OperatingSystem.IsWindows() ? Path.Combine(Environment.SystemDirectory, "sort.exe") : "/bin/cat";

    [Fact]
    public async Task Launch_ReturnsTimeout_OnlyAfterTheHandshakeTimeout()
    {
        var clock = new FakeTimeProvider();
        var launching = PluginProcess.LaunchAsync(SilentLaunch(_silentExecutable), clock, NullLogger.Instance, CancellationToken.None);

        // 进程一直不写握手行：时钟不到时限就一直等，到了才返回超时
        Assert.False(launching.IsCompleted);
        clock.Advance(_handshakeTimeout - TimeSpan.FromMilliseconds(1));
        Assert.False(launching.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        var launched = await launching.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(PluginErrors.Timeout, launched.Error?.Code);
    }

    /// <summary>
    /// 启动一个读 stdin 却不写握手行的进程的参数
    /// </summary>
    private static PluginLaunch SilentLaunch(string executable)
    {
        var host = OperatingSystem.IsWindows()
            ? new PluginEndpoint(PluginEndpoint.NamedPipe, @"\\.\pipe\xihan-silent-host")
            : new PluginEndpoint(PluginEndpoint.Unix, Path.Combine(Path.GetTempPath(), "xihan-silent-host.sock"));
        return new PluginLaunch("xihan.test.silent", executable, Path.GetTempPath(), "silent-session", host, "xihan.test.silent#1", 1, "en-US", _handshakeTimeout);
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Plugins.OutOfProcess.Processes;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 插件 stdout / stderr 的单行长度上限（ADR-0185）
/// </summary>
/// <remarks>
/// 插件不可信：宿主逐行读它的输出，一行不带换行符写上几十 MB，宿主就得把它整行装进内存。用脚本作插件（Windows 上 <c>.cmd</c> 经 <c>type</c> 原样输出文件，Unix 上 <c>/bin/sh</c> 经 <c>cat</c>），输出预先写好的内容
/// </remarks>
public sealed class PluginOutputLimitTests : IAsyncDisposable
{
    /// <summary>
    /// 超长行的字符数
    /// </summary>
    private const int Huge = 8 * 1024 * 1024;

    /// <summary>
    /// 删临时目录时最多等几次
    /// </summary>
    private const int DeleteAttempts = 50;

    /// <summary>
    /// 脚本与输出文件所在的临时目录
    /// </summary>
    /// <remarks>
    /// 每个用例一个，也是脚本插件的工作目录
    /// </remarks>
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("xihan-flood-");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // 这个目录是脚本插件（cmd.exe 或 sh）的工作目录，脚本还读着里面的输出文件。宿主只等到根进程退出，进程树里别的进程（Windows 上的 conhost）的终止是异步的，
        // 全量测试负载高时它们放开目录与文件的句柄要晚一些，删除即以 IOException 失败；稍等再删，最多约 5 秒，仍删不掉照常抛出
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _directory.Delete(recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteAttempts)
            {
                await Task.Delay(100);
            }
        }
    }

    [Fact]
    public async Task OverlongHandshakeLine_IsRejected_WithoutKeepingTheWholeLine()
    {
        var launched = await PluginProcess.LaunchAsync(Launch(new string('x', Huge)), new FakeTimeProvider(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(PluginErrors.InvalidHandshake, launched.Error?.Code);
        // 修之前原因里带着整行（8 MiB）
        Assert.InRange(launched.Error!.Message.Length, 1, 64 * 1024);
    }

    [Fact]
    public async Task OverlongOutputLine_IsLoggedTruncated_AndTheNextLineStillArrives()
    {
        var logs = new LogCapture();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var endpoint = OperatingSystem.IsWindows()
            ? new PluginEndpoint(PluginEndpoint.NamedPipe, @"\\.\pipe\xihan-flood-plugin")
            : new PluginEndpoint(PluginEndpoint.Unix, Path.Combine(_directory.FullName, "plugin.sock"));
        var output = endpoint.ToHandshakeLine() + "\n" + new string('y', Huge) + "\nafter\n";

        var launched = (await PluginProcess.LaunchAsync(Launch(output), new FakeTimeProvider(), factory.CreateLogger("plugin"), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(60))).ValueOrThrow();
        await using (launched)
        {
            for (var attempt = 0; !logs.Entries.Any(static e => e.Message.Contains("after", StringComparison.Ordinal)); attempt++)
            {
                Assert.True(attempt < 600, "The line after the overlong one never arrived.");
                await Task.Delay(50);
            }
        }

        var flooded = Assert.Single(logs.Entries, static e => e.Message.Contains("yyyy", StringComparison.Ordinal));
        Assert.InRange(flooded.Message.Length, PluginProcess.MaxOutputLineChars, PluginProcess.MaxOutputLineChars + 1024);
        Assert.Contains(logs.Entries, static e => e.Level == LogLevel.Warning && e.Message.Contains(PluginProcess.MaxOutputLineChars.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    /// <summary>
    /// 启动一个把 <paramref name="output"/> 原样写到 stdout 的脚本插件的参数
    /// </summary>
    private PluginLaunch Launch(string output)
    {
        var content = Path.Combine(_directory.FullName, "output.txt");
        File.WriteAllText(content, output, new UTF8Encoding(false));
        string script;
        if (OperatingSystem.IsWindows())
        {
            script = Path.Combine(_directory.FullName, "flood.cmd");
            File.WriteAllText(script, $"@type \"{content}\"\r\n");
        }
        else
        {
            script = Path.Combine(_directory.FullName, "flood.sh");
            File.WriteAllText(script, $"#!/bin/sh\ncat '{content}'\n");
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var host = OperatingSystem.IsWindows()
            ? new PluginEndpoint(PluginEndpoint.NamedPipe, @"\\.\pipe\xihan-flood-host")
            : new PluginEndpoint(PluginEndpoint.Unix, Path.Combine(_directory.FullName, "host.sock"));
        return new PluginLaunch("xihan.test.flood", script, _directory.FullName, "flood-session", [host], "xihan.test.flood#1", 1, "en-US", TimeSpan.FromSeconds(30));
    }
}

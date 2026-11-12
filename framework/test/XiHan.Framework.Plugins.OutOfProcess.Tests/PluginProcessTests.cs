// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
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

    [Fact]
    public async Task Launch_KillsTheProcess_WhenCancelledDuringTheHandshake()
    {
        // 用独一无二的文件名启动，才能按进程名认出这次启动的进程，不和并行测试里的同名程序混淆（Linux 的进程名最多 15 个字符）
        var directory = Directory.CreateTempSubdirectory("xihan-silent-");
        var name = "xs" + Guid.NewGuid().ToString("N")[..8];
        var executable = Path.Combine(directory.FullName, OperatingSystem.IsWindows() ? name + ".exe" : name);
        CreateSilentExecutable(executable);
        try
        {
            using var cancellation = new CancellationTokenSource();
            var launching = PluginProcess.LaunchAsync(SilentLaunch(executable), new FakeTimeProvider(), NullLogger.Instance, cancellation.Token);
            Assert.False(launching.IsCompleted);
            Assert.Equal(1, CountRunning(name));

            await cancellation.CancelAsync();

            // 取消照常抛给调用方，但已经启动的进程不能留下
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launching.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal(0, CountRunning(name));
        }
        finally
        {
            KillAll(name);
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// 在 <paramref name="path"/> 建一个读 stdin 却不输出的可执行文件
    /// </summary>
    /// <remarks>
    /// Windows 上复制 <c>sort</c>。Unix 上不复制 <c>cat</c>：Ubuntu 25.10 起的 uutils 是按 <c>argv[0]</c> 分派的多调用程序，改了名的副本认不出自己是 cat，一启动就退出；换成只用内建 <c>read</c> 的 shell 脚本，没有子进程，内核按脚本的文件名设进程名
    /// </remarks>
    private static void CreateSilentExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            File.Copy(_silentExecutable, path);
            return;
        }

        File.WriteAllText(path, "#!/bin/sh\nwhile read -r line; do :; done\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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

    /// <summary>
    /// 按进程名统计仍在运行的进程
    /// </summary>
    private static int CountRunning(string name)
    {
        var processes = Process.GetProcessesByName(name);
        try
        {
            return processes.Count(static process => !process.HasExited);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// 终止按进程名找到的全部进程，测试失败时也不留下子进程
    /// </summary>
    private static void KillAll(string name)
    {
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(TimeSpan.FromSeconds(10));
                }
            }
        }
    }
}

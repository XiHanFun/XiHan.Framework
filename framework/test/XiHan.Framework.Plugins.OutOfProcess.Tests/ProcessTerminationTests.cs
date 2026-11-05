// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Plugins.OutOfProcess.Processes;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 停止超时后的终止
/// </summary>
/// <remarks>
/// <c>spec/plugin/lifecycle.md</c> §5：先发终止信号，5 秒内仍未退出才强制终止进程树
/// 用系统自带的程序作被终止的进程：Windows 上终止信号是关闭 stdin（<c>sort</c> 读到 stdin 结束即退出，<c>ping</c> 不读 stdin），Unix 上另发 SIGTERM（<c>sleep</c> 按缺省动作退出，<c>sh</c> 用 <c>trap '' TERM</c> 忽略它）。信号后的等待按可推进的时钟计时
/// </remarks>
public sealed class ProcessTerminationTests
{
    [Fact]
    public async Task Terminate_SignalsFirst_AndReturnsWhenTheProcessExitsByItself()
    {
        using var process = Start(OperatingSystem.IsWindows() ? (Path.Combine(Environment.SystemDirectory, "sort.exe"), []) : ("sleep", ["60"]));
        var exited = process.WaitForExitAsync();
        var clock = new FakeTimeProvider();

        // 时钟不推进：能返回只因为进程收到终止信号后自行退出，没有被强制终止
        var graceful = await ProcessTermination.TerminateAsync(process, exited, process.StandardInput.Close, clock).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(graceful);
        Assert.True(process.HasExited);
        if (!OperatingSystem.IsWindows())
        {
            // 128 + SIGTERM（15），不是强制终止的 128 + SIGKILL（9）
            Assert.Equal(143, process.ExitCode);
        }
    }

    [Fact]
    public async Task Terminate_KillsTheTree_OnlyAfterTheSignalGrace()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var process = Start(OperatingSystem.IsWindows() ? (Path.Combine(Environment.SystemDirectory, "PING.EXE"), ["-n", "60", "127.0.0.1"]) : ("/bin/sh", ["-c", "trap '' TERM; echo started; sleep 60"]), started);
        var exited = process.WaitForExitAsync();
        var clock = new FakeTimeProvider();

        // 等第一行输出再发信号：shell 还没执行到 trap 时收到 SIGTERM 会直接退出，测的就不是不理会信号的进程了
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var terminating = ProcessTermination.TerminateAsync(process, exited, process.StandardInput.Close, clock);

        // 进程不理会终止信号：时钟不到 5 秒就一直等，到了才强制终止
        Assert.False(terminating.IsCompleted);
        clock.Advance(ProcessTermination.SignalGrace - TimeSpan.FromMilliseconds(1));
        Assert.False(terminating.IsCompleted);
        Assert.False(process.HasExited);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(await terminating.WaitAsync(TimeSpan.FromSeconds(30)));
        await exited.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task Terminate_ReturnsAtOnce_ForAnExitedProcess()
    {
        using var process = Start(OperatingSystem.IsWindows() ? (Path.Combine(Environment.SystemDirectory, "sort.exe"), []) : ("sleep", ["0"]));
        process.StandardInput.Close();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(await ProcessTermination.TerminateAsync(process, Task.CompletedTask, static () => throw new InvalidOperationException("must not signal an exited process"), new FakeTimeProvider()));
    }

    /// <summary>
    /// 启动一个重定向 stdin 的进程，输出被丢弃，第一行输出时完成 firstOutput
    /// </summary>
    private static Process Start((string File, string[] Arguments) command, TaskCompletionSource? firstOutput = null)
    {
        var info = new ProcessStartInfo(command.File)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in command.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var process = Process.Start(info) ?? throw new InvalidOperationException($"'{command.File}' could not be started.");
        process.OutputDataReceived += (_, _) => firstOutput?.TrySetResult();
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XiHan.Framework.Plugins.OutOfProcess.Processes;

/// <summary>
/// 插件进程的终止：先发终止信号，限时内仍未退出再强制终止进程树
/// </summary>
/// <remarks>
/// <c>spec/plugin/lifecycle.md</c> §5：终止信号是关闭 stdin（<c>handshake.md</c> §1 要求插件随之退出），Unix 上同时发 SIGTERM；Windows 没有 SIGTERM，关闭 stdin 就是等价的终止信号。信号后的等待按传入的时钟计时
/// </remarks>
internal static partial class ProcessTermination
{
    /// <summary>
    /// 发出终止信号后等待退出的时限
    /// </summary>
    public static readonly TimeSpan SignalGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 强制终止后等待进程退出的上限
    /// </summary>
    public static readonly TimeSpan KillWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// SIGTERM 的信号值（Linux 与 macOS 相同）
    /// </summary>
    private const int SigTerm = 15;

    /// <summary>
    /// 先发终止信号，限时内仍未退出再终止进程树
    /// </summary>
    /// <remarks>
    /// 终止信号是关闭 stdin，Unix 上同时发 SIGTERM；在 <see cref="SignalGrace"/> 内等待退出。返回进程是否在终止信号之后自行退出（假表示被强制终止）
    /// </remarks>
    /// <param name="process">进程</param>
    /// <param name="exited">进程退出时完成的任务</param>
    /// <param name="closeInput">关闭进程 stdin 的动作</param>
    /// <param name="clock">时钟</param>
    public static async Task<bool> TerminateAsync(Process process, Task exited, Action closeInput, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(exited);
        ArgumentNullException.ThrowIfNull(closeInput);
        ArgumentNullException.ThrowIfNull(clock);
        if (exited.IsCompleted)
        {
            return true;
        }

        closeInput();
        if (!OperatingSystem.IsWindows())
        {
            SignalTerminate(process);
        }

        if (await Task.WhenAny(exited, Task.Delay(SignalGrace, clock)).ConfigureAwait(false) == exited)
        {
            return true;
        }

        await KillAsync(process, exited, clock).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// 强制终止进程树并等待退出（最多 <see cref="KillWait"/>）
    /// </summary>
    public static async Task KillAsync(Process process, Task exited, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(exited);
        ArgumentNullException.ThrowIfNull(clock);
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // 进程已经退出或无法再被操作：按已退出处理
        }

        await Task.WhenAny(exited, Task.Delay(KillWait, clock)).ConfigureAwait(false);
    }

    /// <summary>
    /// 向进程发 SIGTERM
    /// </summary>
    /// <remarks>
    /// 进程已经退出（ESRCH）或运行时找不到 libc 时什么都不做：关闭 stdin 仍是终止信号，限时到了照常强制终止
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    private static void SignalTerminate(Process process)
    {
        try
        {
            _ = NativeMethods.Kill(process.Id, SigTerm);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            // 进程句柄已失效，或平台没有 libc 的 kill
        }
    }

    /// <summary>
    /// libc 导入
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    private static partial class NativeMethods
    {
        /// <summary>
        /// 向进程发信号
        /// </summary>
        [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
        internal static partial int Kill(int processId, int signal);
    }
}

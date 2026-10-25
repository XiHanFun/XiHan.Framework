// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Wasm.Instances;

/// <summary>
/// 宿主进程的影子栈检查：wasmtime 处理陷阱时改写线程上下文，与 Windows 用户态影子栈（CET）冲突，
/// 影子栈强制时第一次陷阱就让进程 fail-fast（0xC0000409），所以在创建引擎前拒绝。
/// </summary>
internal static partial class ShadowStackGuard
{
    /// <summary>影子栈策略位：已启用。</summary>
    private const uint EnableUserShadowStack = 0x1;

    /// <summary>影子栈策略位：只审计、不终止进程。</summary>
    private const uint AuditUserShadowStack = 0x2;

    /// <summary><c>GetProcessMitigationPolicy</c> 的 <c>ProcessUserShadowStackPolicy</c>。</summary>
    private const int ProcessUserShadowStackPolicy = 16;

    /// <summary>
    /// 当前进程是否强制用户态影子栈（已启用且不是审计模式）；非 Windows 或系统没有该策略时为否。
    /// </summary>
    internal static bool IsEnforced()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return IsEnforcedOnWindows();
    }

    /// <summary>
    /// 影子栈强制时抛出配置错误，提示宿主可执行项目关闭 CET 兼容。
    /// </summary>
    /// <exception cref="FrameworkException">当前进程强制用户态影子栈。</exception>
    internal static void EnsureTrapsAreSafe()
    {
        if (IsEnforced())
        {
            throw new FrameworkException(PluginErrors.HostUnsupported, "User-mode shadow stacks (CET) are enforced for this process, so a Wasm trap would terminate it; build the host executable with '<CETCompat>false</CETCompat>'.");
        }
    }

    /// <summary>
    /// 查询 Windows 的进程缓解策略。
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool IsEnforcedOnWindows()
    {
        var buffer = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            Marshal.WriteInt32(buffer, 0);
            // 旧系统没有影子栈策略：查询失败按未启用处理
            if (!NativeMethods.GetProcessMitigationPolicy(NativeMethods.GetCurrentProcess(), ProcessUserShadowStackPolicy, buffer, sizeof(uint)))
            {
                return false;
            }

            var flags = (uint)Marshal.ReadInt32(buffer);
            return (flags & EnableUserShadowStack) != 0 && (flags & AuditUserShadowStack) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// kernel32 导入。
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static partial class NativeMethods
    {
        /// <summary>
        /// 当前进程的伪句柄。
        /// </summary>
        [LibraryImport("kernel32.dll")]
        internal static partial nint GetCurrentProcess();

        /// <summary>
        /// 读取一项进程缓解策略。
        /// </summary>
        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetProcessMitigationPolicy(nint process, int policy, nint buffer, nuint length);
    }
}

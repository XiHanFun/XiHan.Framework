// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 查出哪些进程占着一个目录里的文件
/// </summary>
/// <remarks>
/// 只为删不掉临时目录时的失败信息（ADR-0338）：Windows 上经 Restart Manager（<c>RmGetList</c>）查目录里每个文件的占用者，它只认文件、看不到以这个目录为工作目录的进程；Linux 上读 <c>/proc/&lt;pid&gt;/fd</c> 与 <c>/proc/&lt;pid&gt;/cwd</c>；其余平台不查。查询本身出错时把错误写进结果，不抛出，免得盖住原来的失败
/// </remarks>
internal static partial class FileHolders
{
    /// <summary>
    /// <c>RM_PROCESS_INFO</c> 的字节数
    /// </summary>
    /// <remarks>
    /// <c>RM_UNIQUE_PROCESS</c>（12）、应用名（256 个 UTF-16 码元）、服务短名（64 个）与四个 32 位字段
    /// </remarks>
    private const int ProcessInfoSize = 668;

    /// <summary>
    /// <c>RM_PROCESS_INFO</c> 里应用名的偏移
    /// </summary>
    private const int AppNameOffset = 12;

    /// <summary>
    /// 应用名的 UTF-16 码元数（含结尾的空字符）
    /// </summary>
    private const int AppNameLength = 256;

    /// <summary>
    /// <c>ERROR_MORE_DATA</c>
    /// </summary>
    private const int ErrorMoreData = 234;

    /// <summary>
    /// 描述占着 <paramref name="directory"/> 里文件（Linux 上还有工作目录）的进程与目录里还剩的条目
    /// </summary>
    public static string Describe(string directory)
    {
        string holders;
        try
        {
            holders = OperatingSystem.IsWindows() ? FromRestartManager(directory)
                : OperatingSystem.IsLinux() ? FromProc(directory)
                : "not looked up on this platform";
        }
        catch (Exception exception)
        {
            // 查询只为诊断：出错时说明原因，原来的失败照常抛出
            holders = $"lookup failed with {exception.GetType().Name}: {exception.Message}";
        }

        return $"holders: {holders}; remaining entries: {Remaining(directory)}";
    }

    /// <summary>
    /// 目录里还剩的条目（相对路径）
    /// </summary>
    private static string Remaining(string directory)
    {
        try
        {
            var entries = Directory.Exists(directory)
                ? Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories).Select(entry => Path.GetRelativePath(directory, entry)).ToList()
                : [];
            return entries.Count == 0 ? "none" : string.Join(", ", entries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"not listed ({exception.GetType().Name}: {exception.Message})";
        }
    }

    /// <summary>
    /// 经 Restart Manager 查目录里每个文件的占用进程
    /// </summary>
    private static string FromRestartManager(string directory)
    {
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories) : [];
        if (files.Length == 0)
        {
            return "no file left to look up (Restart Manager does not see working directories)";
        }

        var key = new ushort[33];
        Check(NativeMethods.RmStartSession(out var session, 0, key), "RmStartSession");
        try
        {
            Check(NativeMethods.RmRegisterResources(session, (uint)files.Length, files, 0, 0, 0, 0), "RmRegisterResources");
            uint count = 0;
            var result = NativeMethods.RmGetList(session, out var needed, ref count, null, out _);
            if (result == 0)
            {
                return "none";
            }

            // 第一次只问个数；两次之间又有进程打开文件时再问一次
            byte[] buffer;
            do
            {
                if (result != ErrorMoreData)
                {
                    Check(result, "RmGetList");
                }

                count = needed;
                buffer = new byte[checked((int)needed * ProcessInfoSize)];
                result = NativeMethods.RmGetList(session, out needed, ref count, buffer, out _);
            }
            while (result == ErrorMoreData);

            Check(result, "RmGetList");
            var holders = new List<string>();
            for (var index = 0; index < count; index++)
            {
                var entry = buffer.AsSpan(index * ProcessInfoSize, ProcessInfoSize);
                var processId = BinaryPrimitives.ReadInt32LittleEndian(entry);
                var name = MemoryMarshal.Cast<byte, char>(entry.Slice(AppNameOffset, AppNameLength * 2));
                var end = name.IndexOf('\0');
                holders.Add(Holder(processId, (end >= 0 ? name[..end] : name).ToString()));
            }

            return holders.Count == 0 ? "none" : string.Join(", ", holders);
        }
        finally
        {
            _ = NativeMethods.RmEndSession(session);
        }
    }

    /// <summary>
    /// 读 <c>/proc</c>：文件描述符或工作目录落在目录里的进程
    /// </summary>
    private static string FromProc(string directory)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var holders = new List<string>();
        foreach (var process in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(process), NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
            {
                continue;
            }

            try
            {
                var cwd = new DirectoryInfo(Path.Combine(process, "cwd")).LinkTarget;
                var holds = Inside(cwd, root) || Directory.EnumerateFileSystemEntries(Path.Combine(process, "fd")).Any(fd => Inside(new FileInfo(fd).LinkTarget, root));
                if (holds)
                {
                    holders.Add(Holder(processId, File.ReadAllText(Path.Combine(process, "comm")).Trim()));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 别的用户的进程读不了链接，进程在枚举途中退出也读不了：跳过
            }
        }

        return holders.Count == 0 ? "none" : string.Join(", ", holders);
    }

    /// <summary>
    /// 路径是不是目录本身或在它下面
    /// </summary>
    private static bool Inside(string? path, string root)
        => path is not null && (string.Equals(path, root, StringComparison.Ordinal) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    /// <summary>
    /// 一个占用者的描述：名字、进程 ID，还在运行时带上启动时刻
    /// </summary>
    private static string Holder(int processId, string name)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"{name} (pid {processId}");
        try
        {
            using var process = Process.GetProcessById(processId);
            builder.Append(CultureInfo.InvariantCulture, $", started {process.StartTime.ToUniversalTime():O}");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // 已经退出或读不到启动时刻：只写名字与 ID
        }

        return builder.Append(')').ToString();
    }

    /// <summary>
    /// Restart Manager 的返回值不是成功时抛出
    /// </summary>
    private static void Check(int result, string function)
    {
        if (result != 0)
        {
            throw new IOException($"'{function}' returned {result}.");
        }
    }

    /// <summary>
    /// rstrtmgr 导入
    /// </summary>
    private static partial class NativeMethods
    {
        /// <summary>
        /// 开始一个 Restart Manager 会话
        /// </summary>
        [LibraryImport("rstrtmgr.dll")]
        internal static partial int RmStartSession(out uint sessionHandle, int sessionFlags, ushort[] sessionKey);

        /// <summary>
        /// 登记要查的文件
        /// </summary>
        [LibraryImport("rstrtmgr.dll", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int RmRegisterResources(uint sessionHandle, uint files, string[] fileNames, uint applications, nint uniqueProcesses, uint services, nint serviceNames);

        /// <summary>
        /// 列出占用已登记文件的进程
        /// </summary>
        [LibraryImport("rstrtmgr.dll")]
        internal static partial int RmGetList(uint sessionHandle, out uint processInfoNeeded, ref uint processInfo, byte[]? affectedApps, out uint rebootReasons);

        /// <summary>
        /// 结束会话
        /// </summary>
        [LibraryImport("rstrtmgr.dll")]
        internal static partial int RmEndSession(uint sessionHandle);
    }
}

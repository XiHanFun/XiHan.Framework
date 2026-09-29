// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XiHan.Framework.Observability.Diagnostics;

/// <summary>
/// 诊断服务实现
/// </summary>
public class DiagnosticsService : IDiagnosticsService
{
    private static readonly DateTimeOffset ApplicationStartTime = DateTimeOffset.UtcNow;

    // 运行时长必须用单调时钟量，不能拿墙钟相减：DateTimeOffset.UtcNow 会被 NTP 校时回拨，
    // 一旦在进程启动后发生回拨，UtcNow - ApplicationStartTime 就会算出负的运行时长。
    // ApplicationStartTime 仍保留墙钟值——它是对外展示的「几点启动的」，语义不同。
    private static readonly long StartTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// 获取系统信息
    /// </summary>
    public SystemInfo GetSystemInfo()
    {
        return new SystemInfo
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            OSVersion = Environment.OSVersion.ToString(),
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            SystemStartTime = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64),
            UserName = Environment.UserName
        };
    }

    /// <summary>
    /// 获取运行时信息
    /// </summary>
    public RuntimeInfo GetRuntimeInfo()
    {
        var currentProcess = Process.GetCurrentProcess();
        var uptime = Stopwatch.GetElapsedTime(StartTimestamp);

        return new RuntimeInfo
        {
            DotNetVersion = RuntimeInformation.FrameworkDescription,
            RuntimeVersion = Environment.Version.ToString(),
            ApplicationStartTime = ApplicationStartTime,
            UptimeSeconds = uptime.TotalSeconds,
            ProcessId = currentProcess.Id,
            Is64BitProcess = Environment.Is64BitProcess
        };
    }

    /// <summary>
    /// 获取内存信息
    /// </summary>
    public MemoryInfo GetMemoryInfo()
    {
        var currentProcess = Process.GetCurrentProcess();
        var gcMemoryInfo = GC.GetGCMemoryInfo();

        return new MemoryInfo
        {
            TotalMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes,
            // 不读 GC.GetTotalMemory(false)：regions GC（.NET 7+ 64 位默认）下它按「gen0 跨度 − gen0 碎片」估算，
            // 碎片统计覆盖全部 gen0 region 而跨度只数到临时 region，一旦 gen0 因固定对象保留了额外 region 就无符号下溢，
            // 转成 long 后是负数（dotnet/runtime#130888，CI 上稳定复现）。
            // 改取最近一次 GC 结束时的堆大小减碎片，即对象实际占用的托管堆字节数；口径与 MemoryHealthCheck 一致。
            AllocatedBytes = gcMemoryInfo.HeapSizeBytes - gcMemoryInfo.FragmentedBytes,
            WorkingSetBytes = currentProcess.WorkingSet64,
            PrivateMemoryBytes = currentProcess.PrivateMemorySize64,
            GcInfo = new GCInfo
            {
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalAvailableMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes,
                HighMemoryLoadThresholdBytes = gcMemoryInfo.HighMemoryLoadThresholdBytes,
                PauseTimePercentage = gcMemoryInfo.PauseTimePercentage
            }
        };
    }

    /// <summary>
    /// 获取线程信息
    /// </summary>
    public ThreadInfo GetThreadInfo()
    {
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);

        return new ThreadInfo
        {
            ThreadPoolThreadCount = ThreadPool.ThreadCount,
            AvailableWorkerThreads = availableWorkerThreads,
            AvailableCompletionPortThreads = availableCompletionPortThreads,
            MaxWorkerThreads = maxWorkerThreads,
            MaxCompletionPortThreads = maxCompletionPortThreads,
            PendingWorkItemCount = ThreadPool.PendingWorkItemCount
        };
    }

    /// <summary>
    /// 执行垃圾回收
    /// </summary>
    public void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// 获取完整诊断报告
    /// </summary>
    public DiagnosticsReport GetDiagnosticsReport()
    {
        return new DiagnosticsReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            System = GetSystemInfo(),
            Runtime = GetRuntimeInfo(),
            Memory = GetMemoryInfo(),
            Thread = GetThreadInfo()
        };
    }
}

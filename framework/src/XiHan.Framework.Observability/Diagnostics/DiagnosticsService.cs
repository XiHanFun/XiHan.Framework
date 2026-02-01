#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DiagnosticsService
// Guid:a2b3c4d5-e6f7-48a9-b0c1-d2e3f4a5b6c7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 04:06:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XiHan.Framework.Observability.Diagnostics;

/// <summary>
/// 诊断服务实现
/// </summary>
public class DiagnosticsService : IDiagnosticsService
{
    private static readonly DateTimeOffset ApplicationStartTime = DateTimeOffset.UtcNow;

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
        var uptime = DateTimeOffset.UtcNow - ApplicationStartTime;

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
            AllocatedBytes = GC.GetTotalMemory(false),
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

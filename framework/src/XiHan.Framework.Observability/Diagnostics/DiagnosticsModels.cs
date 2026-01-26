#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DiagnosticsModels
// Guid:f1a2b3c4-d5e6-47f8-a9b0-c1d2e3f4a5b6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26 4:05:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Observability.Diagnostics;

/// <summary>
/// 系统信息
/// </summary>
public class SystemInfo
{
    /// <summary>
    /// 操作系统
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// 操作系统版本
    /// </summary>
    public string OSVersion { get; set; } = string.Empty;

    /// <summary>
    /// 机器名称
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// 处理器数量
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// 系统启动时间
    /// </summary>
    public DateTimeOffset SystemStartTime { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;
}

/// <summary>
/// 运行时信息
/// </summary>
public class RuntimeInfo
{
    /// <summary>
    /// .NET 版本
    /// </summary>
    public string DotNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// 运行时版本
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>
    /// 应用启动时间
    /// </summary>
    public DateTimeOffset ApplicationStartTime { get; set; }

    /// <summary>
    /// 运行时长（秒）
    /// </summary>
    public double UptimeSeconds { get; set; }

    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 是否64位进程
    /// </summary>
    public bool Is64BitProcess { get; set; }
}

/// <summary>
/// 内存信息
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// 总内存（字节）
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// 已分配内存（字节）
    /// </summary>
    public long AllocatedBytes { get; set; }

    /// <summary>
    /// GC 信息
    /// </summary>
    public GCInfo GcInfo { get; set; } = new();

    /// <summary>
    /// 工作集（字节）
    /// </summary>
    public long WorkingSetBytes { get; set; }

    /// <summary>
    /// 私有内存（字节）
    /// </summary>
    public long PrivateMemoryBytes { get; set; }
}

/// <summary>
/// GC 信息
/// </summary>
public class GCInfo
{
    /// <summary>
    /// Gen0 回收次数
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gen1 回收次数
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gen2 回收次数
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// 总可用内存（字节）
    /// </summary>
    public long TotalAvailableMemoryBytes { get; set; }

    /// <summary>
    /// 高内存负载阈值（字节）
    /// </summary>
    public long HighMemoryLoadThresholdBytes { get; set; }

    /// <summary>
    /// 暂停时间百分比
    /// </summary>
    public double PauseTimePercentage { get; set; }
}

/// <summary>
/// 线程信息
/// </summary>
public class ThreadInfo
{
    /// <summary>
    /// 线程池线程数
    /// </summary>
    public int ThreadPoolThreadCount { get; set; }

    /// <summary>
    /// 可用工作线程数
    /// </summary>
    public int AvailableWorkerThreads { get; set; }

    /// <summary>
    /// 可用异步IO线程数
    /// </summary>
    public int AvailableCompletionPortThreads { get; set; }

    /// <summary>
    /// 最大工作线程数
    /// </summary>
    public int MaxWorkerThreads { get; set; }

    /// <summary>
    /// 最大异步IO线程数
    /// </summary>
    public int MaxCompletionPortThreads { get; set; }

    /// <summary>
    /// 挂起的工作项数
    /// </summary>
    public long PendingWorkItemCount { get; set; }
}

/// <summary>
/// 诊断报告
/// </summary>
public class DiagnosticsReport
{
    /// <summary>
    /// 报告生成时间
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 系统信息
    /// </summary>
    public SystemInfo System { get; set; } = new();

    /// <summary>
    /// 运行时信息
    /// </summary>
    public RuntimeInfo Runtime { get; set; } = new();

    /// <summary>
    /// 内存信息
    /// </summary>
    public MemoryInfo Memory { get; set; } = new();

    /// <summary>
    /// 线程信息
    /// </summary>
    public ThreadInfo Thread { get; set; } = new();
}

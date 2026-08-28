// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Observability.Diagnostics;

namespace XiHan.Framework.Observability.Tests.Diagnostics;

/// <summary>
/// 诊断模型测试
/// </summary>
/// <remarks>
/// 这些模型会被诊断接口原样吐给上层（多数场景直接序列化成 JSON 返回），
/// 因此锁「默认值不产生 null 字符串/null 子对象」与「System.Text.Json 往返一致」两件事。
/// </remarks>
public class DiagnosticsModelsTests
{
    /// <summary>
    /// 系统信息默认值：字符串为空串而非 null
    /// </summary>
    [Fact]
    public void SystemInfo_Default_InitializesEmptyStrings()
    {
        var info = new SystemInfo();

        Assert.Equal(string.Empty, info.OperatingSystem);
        Assert.Equal(string.Empty, info.OSVersion);
        Assert.Equal(string.Empty, info.MachineName);
        Assert.Equal(string.Empty, info.UserName);
        Assert.Equal(0, info.ProcessorCount);
        Assert.Equal(default(DateTimeOffset), info.SystemStartTime);
    }

    /// <summary>
    /// 运行时信息默认值
    /// </summary>
    [Fact]
    public void RuntimeInfo_Default_InitializesEmptyStrings()
    {
        var info = new RuntimeInfo();

        Assert.Equal(string.Empty, info.DotNetVersion);
        Assert.Equal(string.Empty, info.RuntimeVersion);
        Assert.Equal(default(DateTimeOffset), info.ApplicationStartTime);
        Assert.Equal(0d, info.UptimeSeconds);
        Assert.Equal(0, info.ProcessId);
        Assert.False(info.Is64BitProcess);
    }

    /// <summary>
    /// 内存信息默认值：GC 子对象默认非空
    /// </summary>
    [Fact]
    public void MemoryInfo_Default_InitializesNonNullGcInfo()
    {
        var info = new MemoryInfo();

        Assert.Equal(0L, info.TotalMemoryBytes);
        Assert.Equal(0L, info.AllocatedBytes);
        Assert.Equal(0L, info.WorkingSetBytes);
        Assert.Equal(0L, info.PrivateMemoryBytes);
        Assert.NotNull(info.GcInfo);
    }

    /// <summary>
    /// 每个内存信息实例持有独立的 GC 子对象
    /// </summary>
    [Fact]
    public void MemoryInfo_GcInfo_IsNotSharedBetweenInstances()
    {
        var first = new MemoryInfo();
        var second = new MemoryInfo();

        Assert.NotSame(first.GcInfo, second.GcInfo);
    }

    /// <summary>
    /// GC 信息默认值
    /// </summary>
    [Fact]
    public void GCInfo_Default_InitializesZeroCounters()
    {
        var info = new GCInfo();

        Assert.Equal(0, info.Gen0Collections);
        Assert.Equal(0, info.Gen1Collections);
        Assert.Equal(0, info.Gen2Collections);
        Assert.Equal(0L, info.TotalAvailableMemoryBytes);
        Assert.Equal(0L, info.HighMemoryLoadThresholdBytes);
        Assert.Equal(0d, info.PauseTimePercentage);
    }

    /// <summary>
    /// 线程信息默认值
    /// </summary>
    [Fact]
    public void ThreadInfo_Default_InitializesZeroCounters()
    {
        var info = new ThreadInfo();

        Assert.Equal(0, info.ThreadPoolThreadCount);
        Assert.Equal(0, info.AvailableWorkerThreads);
        Assert.Equal(0, info.AvailableCompletionPortThreads);
        Assert.Equal(0, info.MaxWorkerThreads);
        Assert.Equal(0, info.MaxCompletionPortThreads);
        Assert.Equal(0L, info.PendingWorkItemCount);
    }

    /// <summary>
    /// 诊断报告默认值：四个分区都已就绪，生成时间取当前 UTC
    /// </summary>
    [Fact]
    public void DiagnosticsReport_Default_InitializesAllSections()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var report = new DiagnosticsReport();

        Assert.NotNull(report.System);
        Assert.NotNull(report.Runtime);
        Assert.NotNull(report.Memory);
        Assert.NotNull(report.Memory.GcInfo);
        Assert.NotNull(report.Thread);
        Assert.InRange(report.GeneratedAt, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    /// <summary>
    /// 每份报告持有独立的分区实例
    /// </summary>
    [Fact]
    public void DiagnosticsReport_Sections_AreNotSharedBetweenInstances()
    {
        var first = new DiagnosticsReport();
        var second = new DiagnosticsReport();

        Assert.NotSame(first.System, second.System);
        Assert.NotSame(first.Runtime, second.Runtime);
        Assert.NotSame(first.Memory, second.Memory);
        Assert.NotSame(first.Thread, second.Thread);
    }

    /// <summary>
    /// 诊断报告经 System.Text.Json 往返后所有分区字段保持一致
    /// </summary>
    [Fact]
    public void DiagnosticsReport_JsonRoundTrip_PreservesAllSections()
    {
        var generatedAt = new DateTimeOffset(2024, 7, 8, 9, 10, 11, TimeSpan.Zero);
        var original = new DiagnosticsReport
        {
            GeneratedAt = generatedAt,
            System = new SystemInfo
            {
                OperatingSystem = "TestOS",
                OSVersion = "1.0",
                MachineName = "box",
                ProcessorCount = 8,
                SystemStartTime = generatedAt.AddHours(-3),
                UserName = "tester"
            },
            Runtime = new RuntimeInfo
            {
                DotNetVersion = ".NET 10.0",
                RuntimeVersion = "10.0.0",
                ApplicationStartTime = generatedAt.AddMinutes(-5),
                UptimeSeconds = 300d,
                ProcessId = 4321,
                Is64BitProcess = true
            },
            Memory = new MemoryInfo
            {
                TotalMemoryBytes = 1_000L,
                AllocatedBytes = 200L,
                WorkingSetBytes = 300L,
                PrivateMemoryBytes = 400L,
                GcInfo = new GCInfo
                {
                    Gen0Collections = 1,
                    Gen1Collections = 2,
                    Gen2Collections = 3,
                    TotalAvailableMemoryBytes = 1_000L,
                    HighMemoryLoadThresholdBytes = 900L,
                    PauseTimePercentage = 1.5d
                }
            },
            Thread = new ThreadInfo
            {
                ThreadPoolThreadCount = 10,
                AvailableWorkerThreads = 20,
                AvailableCompletionPortThreads = 30,
                MaxWorkerThreads = 40,
                MaxCompletionPortThreads = 50,
                PendingWorkItemCount = 60L
            }
        };

        var restored = JsonSerializer.Deserialize<DiagnosticsReport>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal(generatedAt, restored.GeneratedAt);
        Assert.Equal("box", restored.System.MachineName);
        Assert.Equal(8, restored.System.ProcessorCount);
        Assert.Equal("tester", restored.System.UserName);
        Assert.Equal(4321, restored.Runtime.ProcessId);
        Assert.True(restored.Runtime.Is64BitProcess);
        Assert.Equal(300d, restored.Runtime.UptimeSeconds);
        Assert.Equal(1_000L, restored.Memory.TotalMemoryBytes);
        Assert.Equal(3, restored.Memory.GcInfo.Gen2Collections);
        Assert.Equal(1.5d, restored.Memory.GcInfo.PauseTimePercentage);
        Assert.Equal(60L, restored.Thread.PendingWorkItemCount);
        Assert.Equal(40, restored.Thread.MaxWorkerThreads);
    }
}

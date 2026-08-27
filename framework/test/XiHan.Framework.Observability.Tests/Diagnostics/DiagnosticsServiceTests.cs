// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Observability.Diagnostics;

namespace XiHan.Framework.Observability.Tests.Diagnostics;

/// <summary>
/// 诊断服务测试
/// </summary>
/// <remarks>
/// 诊断服务读的是进程与运行时的真实状态，绝对数值不可预设，因此断言只锁三类不变量：
/// 采集项非空、与 Environment/GC 同源读数一致、跨调用的稳定性（如应用启动时间是进程级常量）。
/// </remarks>
public class DiagnosticsServiceTests
{
    /// <summary>
    /// 诊断服务实现诊断契约
    /// </summary>
    [Fact]
    public void DiagnosticsService_Always_ImplementsDiagnosticsContract()
    {
        Assert.IsAssignableFrom<IDiagnosticsService>(new DiagnosticsService());
    }

    /// <summary>
    /// 系统信息采集项非空，且与当前机器一致
    /// </summary>
    [Fact]
    public void GetSystemInfo_Always_ReturnsCurrentMachineIdentity()
    {
        var service = new DiagnosticsService();

        var info = service.GetSystemInfo();

        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info.OperatingSystem));
        Assert.False(string.IsNullOrWhiteSpace(info.OSVersion));
        Assert.Equal(Environment.MachineName, info.MachineName);
        Assert.Equal(Environment.ProcessorCount, info.ProcessorCount);
        Assert.True(info.ProcessorCount >= 1);
        Assert.NotNull(info.UserName);
    }

    /// <summary>
    /// 系统启动时间落在当前时刻之前
    /// </summary>
    [Fact]
    public void GetSystemInfo_SystemStartTime_IsNotInTheFuture()
    {
        var service = new DiagnosticsService();

        var info = service.GetSystemInfo();

        Assert.True(info.SystemStartTime <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 每次采集返回全新实例，调用方修改互不影响
    /// </summary>
    [Fact]
    public void GetSystemInfo_CalledTwice_ReturnsIndependentInstances()
    {
        var service = new DiagnosticsService();

        var first = service.GetSystemInfo();
        var second = service.GetSystemInfo();

        Assert.NotSame(first, second);
        Assert.Equal(first.MachineName, second.MachineName);
    }

    /// <summary>
    /// 运行时信息描述的是当前进程
    /// </summary>
    [Fact]
    public void GetRuntimeInfo_Always_DescribesCurrentProcess()
    {
        var service = new DiagnosticsService();

        var info = service.GetRuntimeInfo();

        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info.DotNetVersion));
        Assert.Contains(".NET", info.DotNetVersion);
        Assert.Equal(Environment.Version.ToString(), info.RuntimeVersion);
        Assert.Equal(Environment.ProcessId, info.ProcessId);
        Assert.Equal(Environment.Is64BitProcess, info.Is64BitProcess);
        Assert.True(info.UptimeSeconds >= 0d);
    }

    /// <summary>
    /// 应用启动时间是进程级常量，跨调用、跨实例都相同
    /// </summary>
    [Fact]
    public void GetRuntimeInfo_ApplicationStartTime_IsProcessWideConstant()
    {
        var first = new DiagnosticsService().GetRuntimeInfo();
        var second = new DiagnosticsService().GetRuntimeInfo();

        Assert.Equal(first.ApplicationStartTime, second.ApplicationStartTime);
        Assert.True(first.ApplicationStartTime <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 运行时长随时间单调不减
    /// </summary>
    [Fact]
    public void GetRuntimeInfo_UptimeSeconds_IsNonDecreasingOverTime()
    {
        var service = new DiagnosticsService();

        var first = service.GetRuntimeInfo();
        Thread.Sleep(20);
        var second = service.GetRuntimeInfo();

        Assert.True(second.UptimeSeconds >= first.UptimeSeconds);
    }

    /// <summary>
    /// 内存信息各项读数为正，GC 计数非负
    /// </summary>
    [Fact]
    public void GetMemoryInfo_Always_ReturnsPositiveFigures()
    {
        var service = new DiagnosticsService();

        var info = service.GetMemoryInfo();

        Assert.NotNull(info);
        Assert.True(info.AllocatedBytes > 0);
        Assert.True(info.WorkingSetBytes > 0);
        Assert.True(info.PrivateMemoryBytes >= 0);
        Assert.True(info.TotalMemoryBytes > 0);
        Assert.NotNull(info.GcInfo);
        Assert.True(info.GcInfo.Gen0Collections >= 0);
        Assert.True(info.GcInfo.Gen1Collections >= 0);
        Assert.True(info.GcInfo.Gen2Collections >= 0);
        Assert.True(info.GcInfo.PauseTimePercentage >= 0d);
    }

    /// <summary>
    /// 总内存与 GC 信息里的总可用内存取自同一次读数，必须完全相等
    /// </summary>
    [Fact]
    public void GetMemoryInfo_TotalMemoryBytes_MirrorsGcTotalAvailableMemory()
    {
        var service = new DiagnosticsService();

        var info = service.GetMemoryInfo();

        Assert.Equal(info.GcInfo.TotalAvailableMemoryBytes, info.TotalMemoryBytes);
        Assert.True(info.GcInfo.HighMemoryLoadThresholdBytes > 0);
    }

    /// <summary>
    /// 线程信息中的可用线程数不超过最大线程数
    /// </summary>
    [Fact]
    public void GetThreadInfo_Always_KeepsThreadPoolCountersConsistent()
    {
        var service = new DiagnosticsService();

        var info = service.GetThreadInfo();

        Assert.NotNull(info);
        Assert.True(info.MaxWorkerThreads > 0);
        Assert.True(info.MaxCompletionPortThreads > 0);
        Assert.True(info.AvailableWorkerThreads >= 0);
        Assert.True(info.AvailableCompletionPortThreads >= 0);
        Assert.True(info.AvailableWorkerThreads <= info.MaxWorkerThreads);
        Assert.True(info.AvailableCompletionPortThreads <= info.MaxCompletionPortThreads);
        Assert.True(info.ThreadPoolThreadCount >= 0);
        Assert.True(info.PendingWorkItemCount >= 0);
    }

    /// <summary>
    /// 强制回收会推进第二代回收计数
    /// </summary>
    [Fact]
    public void ForceGarbageCollection_Always_AdvancesGen2CollectionCount()
    {
        var service = new DiagnosticsService();
        var before = GC.CollectionCount(2);

        service.ForceGarbageCollection();

        Assert.True(GC.CollectionCount(2) > before);
    }

    /// <summary>
    /// 诊断报告聚合四个分区，且分区内容与单项采集一致
    /// </summary>
    [Fact]
    public void GetDiagnosticsReport_Always_AggregatesAllSections()
    {
        var service = new DiagnosticsService();

        var report = service.GetDiagnosticsReport();

        Assert.NotNull(report.System);
        Assert.NotNull(report.Runtime);
        Assert.NotNull(report.Memory);
        Assert.NotNull(report.Memory.GcInfo);
        Assert.NotNull(report.Thread);
        Assert.Equal(Environment.MachineName, report.System.MachineName);
        Assert.Equal(Environment.ProcessId, report.Runtime.ProcessId);
        Assert.True(report.Memory.AllocatedBytes > 0);
        Assert.True(report.Thread.MaxWorkerThreads > 0);
    }

    /// <summary>
    /// 报告生成时间取当次 UTC 时刻
    /// </summary>
    [Fact]
    public void GetDiagnosticsReport_GeneratedAt_IsCurrentUtcMoment()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var report = new DiagnosticsService().GetDiagnosticsReport();

        Assert.InRange(report.GeneratedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    /// <summary>
    /// 每次生成报告返回全新实例
    /// </summary>
    [Fact]
    public void GetDiagnosticsReport_CalledTwice_ReturnsIndependentInstances()
    {
        var service = new DiagnosticsService();

        var first = service.GetDiagnosticsReport();
        var second = service.GetDiagnosticsReport();

        Assert.NotSame(first, second);
        Assert.NotSame(first.System, second.System);
        Assert.NotSame(first.Memory, second.Memory);
    }
}

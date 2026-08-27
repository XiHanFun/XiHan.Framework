// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Configuration;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Configuration;

/// <summary>
/// XiHanJobOptions 配置选项测试
/// </summary>
/// <remarks>
/// 配置节名称是外部 appsettings 的对接约定，改了会导致所有既有配置静默失效，必须锁死。
/// 其余默认值决定"不配置时的行为"，同样属于对外承诺。
/// </remarks>
public class XiHanJobOptionsTests
{
    /// <summary>
    /// 配置节名称是外部约定，不得随意变更
    /// </summary>
    [Fact]
    public void SectionName_IsStableContract()
    {
        Assert.Equal("XiHan:Tasks:ScheduledJobs", XiHanJobOptions.SectionName);
    }

    /// <summary>
    /// 不配置时默认启用调度、启用自动发现、启用度量
    /// </summary>
    [Fact]
    public void Constructor_Default_EnablesSchedulingDiscoveryAndMetrics()
    {
        var options = new XiHanJobOptions();

        Assert.True(options.Enabled);
        Assert.True(options.AutoDiscoverJobs);
        Assert.True(options.EnableMetrics);
    }

    /// <summary>
    /// 默认超时 5 分钟、历史保留 30 天，与 JobInfo 的默认超时保持一致口径
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesFiveMinuteTimeoutAndThirtyDayRetention()
    {
        var options = new XiHanJobOptions();

        Assert.Equal(300000, options.DefaultTimeoutMilliseconds);
        Assert.Equal(30, options.HistoryRetentionDays);
    }

    /// <summary>
    /// 默认扫描 *.Jobs 与 *.Tasks 两类程序集
    /// </summary>
    [Fact]
    public void Constructor_Default_ScansJobsAndTasksAssemblies()
    {
        var options = new XiHanJobOptions();

        Assert.Equal(new[] { "*.Jobs", "*.Tasks" }, options.JobAssemblyPatterns);
    }

    /// <summary>
    /// 节点名称默认为空，表示由运行环境决定
    /// </summary>
    [Fact]
    public void Constructor_Default_LeavesNodeNameUnset()
    {
        Assert.Null(new XiHanJobOptions().NodeName);
    }

    /// <summary>
    /// 两个选项实例各自持有独立的扫描模式数组
    /// </summary>
    [Fact]
    public void JobAssemblyPatterns_OnDifferentInstances_AreNotShared()
    {
        var first = new XiHanJobOptions();
        var second = new XiHanJobOptions();

        first.JobAssemblyPatterns[0] = "*.Changed";

        Assert.Equal("*.Jobs", second.JobAssemblyPatterns[0]);
    }

    /// <summary>
    /// 每个开关都可被显式关闭
    /// </summary>
    [Fact]
    public void Switches_CanBeTurnedOff()
    {
        var options = new XiHanJobOptions
        {
            Enabled = false,
            AutoDiscoverJobs = false,
            EnableMetrics = false,
            NodeName = "node-1",
            HistoryRetentionDays = 7,
            DefaultTimeoutMilliseconds = 1000,
            JobAssemblyPatterns = ["*.Custom"]
        };

        Assert.False(options.Enabled);
        Assert.False(options.AutoDiscoverJobs);
        Assert.False(options.EnableMetrics);
        Assert.Equal("node-1", options.NodeName);
        Assert.Equal(7, options.HistoryRetentionDays);
        Assert.Equal(1000, options.DefaultTimeoutMilliseconds);
        Assert.Equal(new[] { "*.Custom" }, options.JobAssemblyPatterns);
    }
}

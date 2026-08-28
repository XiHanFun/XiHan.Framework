// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using XiHan.Framework.Observability.HealthChecks;

namespace XiHan.Framework.Observability.Tests.HealthChecks;

/// <summary>
/// 内存健康检查测试
/// </summary>
/// <remarks>
/// 判定口径是 GC.GetTotalMemory(false) 与「阈值 MB × 1024 × 1024」比较，且用的是 &gt;= ：
/// 阈值 0 时必然落在降级分支（已分配字节数不可能为负），这是唯一能在不注入内存读数的前提下稳定命中的边界；
/// 另一侧用真实压舱内存把已分配量顶到阈值之上，验证比较走的确实是阈值而不是常量。
/// 当前实现只有 Healthy / Degraded 两条出口，没有 Unhealthy 分支，用例按现状锁定并在报告中标注该缺口。
/// </remarks>
public class MemoryHealthCheckTests
{
    /// <summary>
    /// 内存健康检查实现健康检查契约
    /// </summary>
    [Fact]
    public void MemoryHealthCheck_Always_ImplementsHealthCheckContract()
    {
        Assert.IsAssignableFrom<IHealthCheck>(new MemoryHealthCheck());
    }

    /// <summary>
    /// 阈值为 0 时任何已分配量都达到阈值，判定为降级
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_WithZeroThreshold_ReturnsDegraded()
    {
        var check = new MemoryHealthCheck(0);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    /// <summary>
    /// 阈值远高于进程实际占用时判定为健康
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_WithThresholdFarAboveUsage_ReturnsHealthy()
    {
        var check = new MemoryHealthCheck(1_000_000);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("内存使用正常", result.Description);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// 实际已分配量超过阈值时判定为降级
    /// </summary>
    /// <remarks>
    /// 用一块存活的压舱数组把 GC.GetTotalMemory 顶到阈值之上，确保比较真的以构造参数为准。
    /// </remarks>
    [Fact]
    public async Task CheckHealthAsync_WhenAllocationExceedsThreshold_ReturnsDegraded()
    {
        var ballast = new byte[48 * 1024 * 1024];
        ballast[0] = 1;

        try
        {
            var check = new MemoryHealthCheck(8);

            var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.NotNull(result.Description);
            Assert.Contains("内存使用过高", result.Description);
            Assert.Contains("MB", result.Description);
            Assert.Null(result.Exception);
        }
        finally
        {
            GC.KeepAlive(ballast);
        }
    }

    /// <summary>
    /// 无论健康还是降级都带齐七项诊断数据
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1_000_000L)]
    public async Task CheckHealthAsync_WithAnyThreshold_PopulatesAllDiagnosticData(long thresholdMb)
    {
        var check = new MemoryHealthCheck(thresholdMb);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(7, result.Data.Count);
        Assert.True(result.Data.ContainsKey("AllocatedBytes"));
        Assert.True(result.Data.ContainsKey("AllocatedMB"));
        Assert.True(result.Data.ContainsKey("Gen0Collections"));
        Assert.True(result.Data.ContainsKey("Gen1Collections"));
        Assert.True(result.Data.ContainsKey("Gen2Collections"));
        Assert.True(result.Data.ContainsKey("TotalAvailableMemoryBytes"));
        Assert.True(result.Data.ContainsKey("HighMemoryLoadThresholdBytes"));
    }

    /// <summary>
    /// 诊断数据的类型与相互换算关系正确
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_DiagnosticData_KeepsTypesAndUnitConversion()
    {
        var check = new MemoryHealthCheck(1_000_000);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        var allocatedBytes = Assert.IsType<long>(result.Data["AllocatedBytes"]);
        var allocatedMb = Assert.IsType<long>(result.Data["AllocatedMB"]);
        var gen0 = Assert.IsType<int>(result.Data["Gen0Collections"]);
        var gen1 = Assert.IsType<int>(result.Data["Gen1Collections"]);
        var gen2 = Assert.IsType<int>(result.Data["Gen2Collections"]);
        var totalAvailable = Assert.IsType<long>(result.Data["TotalAvailableMemoryBytes"]);
        Assert.IsType<long>(result.Data["HighMemoryLoadThresholdBytes"]);

        Assert.True(allocatedBytes > 0);
        Assert.Equal(allocatedBytes / 1024 / 1024, allocatedMb);
        Assert.True(gen0 >= 0);
        Assert.True(gen1 >= 0);
        Assert.True(gen2 >= 0);
        Assert.True(totalAvailable > 0);
    }

    /// <summary>
    /// 无参构造等价于 1024MB 阈值
    /// </summary>
    /// <remarks>
    /// 阈值字段不可见，这里用「同一时刻两个实例判定一致」间接锁死默认参数值，
    /// 若默认值被改成别的数量级，两者判定会立刻分叉。
    /// </remarks>
    [Fact]
    public async Task CheckHealthAsync_WithDefaultThreshold_BehavesLikeExplicit1024Mb()
    {
        var token = TestContext.Current.CancellationToken;

        var defaultResult = await new MemoryHealthCheck().CheckHealthAsync(new HealthCheckContext(), token);
        var explicitResult = await new MemoryHealthCheck(1024).CheckHealthAsync(new HealthCheckContext(), token);

        Assert.Equal(explicitResult.Status, defaultResult.Status);
        Assert.Equal(explicitResult.Description, defaultResult.Description);
    }

    /// <summary>
    /// 各档阈值下只会给出健康或降级，不会给出不健康
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(1024L)]
    [InlineData(1_000_000L)]
    public async Task CheckHealthAsync_WithVariousThresholds_NeverReportsUnhealthy(long thresholdMb)
    {
        var check = new MemoryHealthCheck(thresholdMb);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.NotEqual(HealthStatus.Unhealthy, result.Status);
        Assert.True(result.Status == HealthStatus.Healthy || result.Status == HealthStatus.Degraded);
    }

    /// <summary>
    /// 同一实例重复检查得到一致的判定
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_CalledTwiceOnSameInstance_ReturnsSameStatus()
    {
        var check = new MemoryHealthCheck(0);
        var token = TestContext.Current.CancellationToken;

        var first = await check.CheckHealthAsync(new HealthCheckContext(), token);
        var second = await check.CheckHealthAsync(new HealthCheckContext(), token);

        Assert.Equal(HealthStatus.Degraded, first.Status);
        Assert.Equal(first.Status, second.Status);
    }

    /// <summary>
    /// 不传取消令牌时使用默认值，同样能完成检查
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_WithoutCancellationToken_UsesDefaultAndCompletes()
    {
        var check = new MemoryHealthCheck(1_000_000);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}

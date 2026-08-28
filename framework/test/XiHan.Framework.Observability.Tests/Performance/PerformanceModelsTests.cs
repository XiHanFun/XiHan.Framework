// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Observability.Performance;

namespace XiHan.Framework.Observability.Tests.Performance;

/// <summary>
/// 性能模型测试
/// </summary>
/// <remarks>
/// PerformanceRecord / Checkpoint / PerformanceStatistics / OperationStatistics 是会被接口原样吐出的契约模型，
/// 锁默认值（尤其 Success 默认 true、集合默认非空）与 System.Text.Json 往返一致性。
/// </remarks>
public class PerformanceModelsTests
{
    /// <summary>
    /// 性能记录默认值：集合非空、默认成功、结束时间未定
    /// </summary>
    [Fact]
    public void PerformanceRecord_Default_InitializesSafeDefaults()
    {
        var perfRecord = new PerformanceRecord();

        Assert.Equal(string.Empty, perfRecord.OperationName);
        Assert.Equal(default(DateTimeOffset), perfRecord.StartTime);
        Assert.Null(perfRecord.EndTime);
        Assert.Equal(0d, perfRecord.DurationMs);
        Assert.NotNull(perfRecord.Tags);
        Assert.Empty(perfRecord.Tags);
        Assert.NotNull(perfRecord.Checkpoints);
        Assert.Empty(perfRecord.Checkpoints);
        Assert.True(perfRecord.Success);
        Assert.Null(perfRecord.Exception);
    }

    /// <summary>
    /// 每条记录持有独立的标签与检查点集合
    /// </summary>
    [Fact]
    public void PerformanceRecord_Collections_AreNotSharedBetweenInstances()
    {
        var first = new PerformanceRecord();
        var second = new PerformanceRecord();

        first.Tags["k"] = "v";
        first.Checkpoints.Add(new Checkpoint { Name = "c" });

        Assert.NotSame(first.Tags, second.Tags);
        Assert.NotSame(first.Checkpoints, second.Checkpoints);
        Assert.Empty(second.Tags);
        Assert.Empty(second.Checkpoints);
    }

    /// <summary>
    /// 检查点默认值
    /// </summary>
    [Fact]
    public void Checkpoint_Default_InitializesSafeDefaults()
    {
        var checkpoint = new Checkpoint();

        Assert.Equal(string.Empty, checkpoint.Name);
        Assert.Equal(default(DateTimeOffset), checkpoint.Timestamp);
        Assert.Equal(0d, checkpoint.ElapsedMs);
    }

    /// <summary>
    /// 性能统计默认值：计数与耗时全零、操作分组非空
    /// </summary>
    [Fact]
    public void PerformanceStatistics_Default_InitializesZeroedSnapshot()
    {
        var statistics = new PerformanceStatistics();

        Assert.Equal(0, statistics.TotalOperations);
        Assert.Equal(0, statistics.SuccessfulOperations);
        Assert.Equal(0, statistics.FailedOperations);
        Assert.Equal(0d, statistics.AverageDurationMs);
        Assert.Equal(0d, statistics.MinDurationMs);
        Assert.Equal(0d, statistics.MaxDurationMs);
        Assert.Equal(0d, statistics.P50DurationMs);
        Assert.Equal(0d, statistics.P95DurationMs);
        Assert.Equal(0d, statistics.P99DurationMs);
        Assert.NotNull(statistics.OperationStats);
        Assert.Empty(statistics.OperationStats);
    }

    /// <summary>
    /// 操作统计默认值
    /// </summary>
    [Fact]
    public void OperationStatistics_Default_InitializesSafeDefaults()
    {
        var operation = new OperationStatistics();

        Assert.Equal(string.Empty, operation.OperationName);
        Assert.Equal(0, operation.Count);
        Assert.Equal(0d, operation.AverageDurationMs);
        Assert.Equal(0d, operation.MinDurationMs);
        Assert.Equal(0d, operation.MaxDurationMs);
    }

    /// <summary>
    /// 性能模型是普通类，内容相同的两个实例不相等
    /// </summary>
    [Fact]
    public void Equals_TwoRecordsWithSameContent_UsesReferenceSemantics()
    {
        var first = new PerformanceRecord { OperationName = "op" };
        var second = new PerformanceRecord { OperationName = "op" };

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 性能记录经 System.Text.Json 往返后所有字段保持一致
    /// </summary>
    [Fact]
    public void PerformanceRecord_JsonRoundTrip_PreservesAllFields()
    {
        var start = new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var original = new PerformanceRecord
        {
            OperationName = "save-order",
            StartTime = start,
            EndTime = start.AddMilliseconds(120),
            DurationMs = 120d,
            Success = false,
            Exception = "boom",
            Tags = new Dictionary<string, string> { ["tenant"] = "t1" },
            Checkpoints = [new Checkpoint { Name = "validated", Timestamp = start.AddMilliseconds(30), ElapsedMs = 30d }]
        };

        var restored = JsonSerializer.Deserialize<PerformanceRecord>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal("save-order", restored.OperationName);
        Assert.Equal(start, restored.StartTime);
        Assert.NotNull(restored.EndTime);
        Assert.Equal(start.AddMilliseconds(120), restored.EndTime.Value);
        Assert.Equal(120d, restored.DurationMs);
        Assert.False(restored.Success);
        Assert.Equal("boom", restored.Exception);
        Assert.Equal("t1", restored.Tags["tenant"]);
        Assert.Single(restored.Checkpoints);
        Assert.Equal("validated", restored.Checkpoints[0].Name);
        Assert.Equal(30d, restored.Checkpoints[0].ElapsedMs);
    }

    /// <summary>
    /// 结束时间为空时往返后仍为空
    /// </summary>
    [Fact]
    public void PerformanceRecord_JsonRoundTrip_WithNullEndTime_KeepsNull()
    {
        var restored = JsonSerializer.Deserialize<PerformanceRecord>(JsonSerializer.Serialize(new PerformanceRecord()));

        Assert.NotNull(restored);
        Assert.Null(restored.EndTime);
        Assert.Null(restored.Exception);
        Assert.True(restored.Success);
    }

    /// <summary>
    /// 性能统计经 System.Text.Json 往返后分组信息保持一致
    /// </summary>
    [Fact]
    public void PerformanceStatistics_JsonRoundTrip_PreservesOperationStats()
    {
        var original = new PerformanceStatistics
        {
            TotalOperations = 3,
            SuccessfulOperations = 2,
            FailedOperations = 1,
            AverageDurationMs = 10d,
            MinDurationMs = 5d,
            MaxDurationMs = 20d,
            P50DurationMs = 8d,
            P95DurationMs = 19d,
            P99DurationMs = 20d,
            OperationStats = new Dictionary<string, OperationStatistics>
            {
                ["alpha"] = new OperationStatistics
                {
                    OperationName = "alpha",
                    Count = 2,
                    AverageDurationMs = 7.5d,
                    MinDurationMs = 5d,
                    MaxDurationMs = 10d
                }
            }
        };

        var restored = JsonSerializer.Deserialize<PerformanceStatistics>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal(3, restored.TotalOperations);
        Assert.Equal(2, restored.SuccessfulOperations);
        Assert.Equal(1, restored.FailedOperations);
        Assert.Equal(20d, restored.P99DurationMs);
        Assert.Single(restored.OperationStats);
        Assert.Equal("alpha", restored.OperationStats["alpha"].OperationName);
        Assert.Equal(2, restored.OperationStats["alpha"].Count);
        Assert.Equal(7.5d, restored.OperationStats["alpha"].AverageDurationMs);
    }
}

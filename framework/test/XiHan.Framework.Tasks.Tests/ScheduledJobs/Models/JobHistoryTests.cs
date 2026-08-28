// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Models;

/// <summary>
/// JobHistory 执行历史模型测试
/// </summary>
/// <remarks>
/// 历史记录是要落库并跨进程读取的，除了默认值以外还做一次 System.Text.Json 往返，
/// 确认全部字段都是可序列化的普通类型、可空性在往返后保持不变。
/// </remarks>
public class JobHistoryTests
{
    /// <summary>
    /// 新建历史记录的可空字段全空，计数字段为零
    /// </summary>
    [Fact]
    public void Constructor_Default_LeavesOptionalFieldsEmpty()
    {
        var history = new JobHistory();

        Assert.Equal(string.Empty, history.InstanceId);
        Assert.Equal(string.Empty, history.JobName);
        Assert.Equal(JobStatus.Pending, history.Status);
        Assert.Null(history.CompletedAt);
        Assert.Null(history.DurationMilliseconds);
        Assert.Null(history.TenantId);
        Assert.Equal(JobTriggerType.Cron, history.TriggerType);
        Assert.False(history.IsSuccess);
        Assert.Null(history.ErrorMessage);
        Assert.Null(history.StackTrace);
        Assert.Equal(0, history.RetryCount);
        Assert.Null(history.ExecutionNode);
        Assert.Null(history.TraceId);
        Assert.Null(history.ParametersJson);
        Assert.Null(history.Remarks);
    }

    /// <summary>
    /// 历史唯一标识自动生成为 32 位十六进制串且互不重复
    /// </summary>
    [Fact]
    public void HistoryId_IsGeneratedAndUnique()
    {
        var history = new JobHistory();

        Assert.Equal(32, history.HistoryId.Length);
        Assert.True(history.HistoryId.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));

        var ids = Enumerable.Range(0, 200).Select(_ => new JobHistory().HistoryId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    /// 完整填充后的历史记录经过 JSON 往返仍保持全部字段
    /// </summary>
    [Fact]
    public void SerializeAndDeserialize_RoundTrip_PreservesEveryField()
    {
        var original = new JobHistory
        {
            InstanceId = "instance-1",
            JobName = "job-a",
            Status = JobStatus.Failed,
            StartedAt = new DateTimeOffset(2024, 6, 12, 8, 0, 0, TimeSpan.Zero),
            CompletedAt = new DateTimeOffset(2024, 6, 12, 8, 0, 5, TimeSpan.Zero),
            DurationMilliseconds = 5000,
            TenantId = 42L,
            TriggerType = JobTriggerType.Interval,
            IsSuccess = false,
            ErrorMessage = "失败原因",
            StackTrace = "堆栈",
            RetryCount = 2,
            ExecutionNode = "node-1",
            TraceId = "trace-1",
            ParametersJson = "{\"batchSize\":100}",
            Remarks = "备注"
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<JobHistory>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.HistoryId, restored!.HistoryId);
        Assert.Equal(original.InstanceId, restored.InstanceId);
        Assert.Equal(original.JobName, restored.JobName);
        Assert.Equal(original.Status, restored.Status);
        Assert.Equal(original.StartedAt, restored.StartedAt);
        Assert.Equal(original.CompletedAt, restored.CompletedAt);
        Assert.Equal(original.DurationMilliseconds, restored.DurationMilliseconds);
        Assert.Equal(original.TenantId, restored.TenantId);
        Assert.Equal(original.TriggerType, restored.TriggerType);
        Assert.Equal(original.IsSuccess, restored.IsSuccess);
        Assert.Equal(original.ErrorMessage, restored.ErrorMessage);
        Assert.Equal(original.StackTrace, restored.StackTrace);
        Assert.Equal(original.RetryCount, restored.RetryCount);
        Assert.Equal(original.ExecutionNode, restored.ExecutionNode);
        Assert.Equal(original.TraceId, restored.TraceId);
        Assert.Equal(original.ParametersJson, restored.ParametersJson);
        Assert.Equal(original.Remarks, restored.Remarks);
    }

    /// <summary>
    /// 可空字段为空时往返后依旧为空，不会被序列化器填成默认值
    /// </summary>
    [Fact]
    public void SerializeAndDeserialize_WithNullableFieldsUnset_KeepsThemNull()
    {
        var original = new JobHistory { JobName = "job-a" };

        var restored = JsonSerializer.Deserialize<JobHistory>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Null(restored!.CompletedAt);
        Assert.Null(restored.DurationMilliseconds);
        Assert.Null(restored.TenantId);
        Assert.Null(restored.ErrorMessage);
        Assert.Null(restored.ParametersJson);
    }
}

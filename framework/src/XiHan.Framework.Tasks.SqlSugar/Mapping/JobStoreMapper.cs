// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Mapping;

/// <summary>
/// 定时任务实例与执行历史的契约与实体双向映射
/// </summary>
/// <remarks>
/// 实体中的时间均以协调世界时存储，读回时还原为偏移为零的 <see cref="DateTimeOffset"/>。
/// </remarks>
public static class JobStoreMapper
{
    /// <summary>
    /// 不限时任务的运行截止时刻，表示实例在被显式结束之前一直视为运行中
    /// </summary>
    public static readonly DateTime UnboundedRunningDeadline = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 把任务实例转换为实体
    /// </summary>
    /// <remarks>
    /// 运行中状态的实例计算运行截止时刻：开始时间（为空时取计划时间）加任务超时再加宽限期；
    /// 任务超时小于等于 0 时取 <see cref="UnboundedRunningDeadline"/>。
    /// 执行参数序列化抛出 <see cref="NotSupportedException"/> 或 <see cref="JsonException"/> 时存为空。
    /// </remarks>
    /// <param name="instance">任务实例</param>
    /// <param name="runningGracePeriod">运行中实例的宽限期</param>
    /// <returns>任务实例实体</returns>
    public static SysJobInstance ToEntity(JobInstance instance, TimeSpan runningGracePeriod)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return new SysJobInstance(instance.InstanceId)
        {
            JobName = instance.JobName,
            JobTypeName = instance.JobInfo?.JobType?.AssemblyQualifiedName,
            Status = (int)instance.Status,
            TriggerType = (int)instance.TriggerType,
            TenantId = instance.TenantId,
            ScheduledAt = ToUtc(instance.ScheduledAt),
            StartedAt = ToUtc(instance.StartedAt),
            CompletedAt = ToUtc(instance.CompletedAt),
            DurationMilliseconds = instance.DurationMilliseconds,
            RunningDeadline = ComputeRunningDeadline(instance, runningGracePeriod),
            RetryCount = instance.RetryCount,
            ExecutionNode = instance.ExecutionNode,
            TraceId = instance.TraceId,
            ParametersJson = SerializeParameters(instance.Parameters),
            ErrorMessage = instance.ErrorMessage,
            StackTrace = instance.StackTrace
        };
    }

    /// <summary>
    /// 把实体转换为任务实例
    /// </summary>
    /// <remarks>
    /// 任务信息只还原任务名称、任务类型（能解析时）、触发类型与租户；执行参数的值还原为 <see cref="JsonElement"/>。
    /// </remarks>
    /// <param name="entity">任务实例实体</param>
    /// <returns>任务实例</returns>
    public static JobInstance ToJobInstance(SysJobInstance entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var jobInfo = new JobInfo
        {
            JobName = entity.JobName,
            TriggerType = (JobTriggerType)entity.TriggerType,
            TenantId = entity.TenantId
        };

        var jobType = ResolveType(entity.JobTypeName);
        if (jobType is not null)
        {
            jobInfo.JobType = jobType;
        }

        return new JobInstance
        {
            InstanceId = entity.BasicId,
            JobName = entity.JobName,
            JobInfo = jobInfo,
            Status = (JobStatus)entity.Status,
            ScheduledAt = FromUtc(entity.ScheduledAt),
            StartedAt = FromUtc(entity.StartedAt),
            CompletedAt = FromUtc(entity.CompletedAt),
            DurationMilliseconds = entity.DurationMilliseconds,
            TriggerType = (JobTriggerType)entity.TriggerType,
            TenantId = entity.TenantId,
            Parameters = DeserializeParameters(entity.ParametersJson),
            ErrorMessage = entity.ErrorMessage,
            StackTrace = entity.StackTrace,
            RetryCount = entity.RetryCount,
            ExecutionNode = entity.ExecutionNode,
            TraceId = entity.TraceId
        };
    }

    /// <summary>
    /// 把执行历史转换为实体
    /// </summary>
    /// <param name="history">执行历史</param>
    /// <returns>执行历史实体</returns>
    public static SysJobHistory ToEntity(JobHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return new SysJobHistory(history.HistoryId)
        {
            InstanceId = history.InstanceId,
            JobName = history.JobName,
            Status = (int)history.Status,
            StartedAt = ToUtc(history.StartedAt),
            CompletedAt = ToUtc(history.CompletedAt),
            DurationMilliseconds = history.DurationMilliseconds,
            TenantId = history.TenantId,
            TriggerType = (int)history.TriggerType,
            IsSuccess = history.IsSuccess,
            ErrorMessage = history.ErrorMessage,
            StackTrace = history.StackTrace,
            RetryCount = history.RetryCount,
            ExecutionNode = history.ExecutionNode,
            TraceId = history.TraceId,
            ParametersJson = history.ParametersJson,
            Remarks = history.Remarks
        };
    }

    /// <summary>
    /// 把实体转换为执行历史
    /// </summary>
    /// <param name="entity">执行历史实体</param>
    /// <returns>执行历史</returns>
    public static JobHistory ToJobHistory(SysJobHistory entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new JobHistory
        {
            HistoryId = entity.BasicId,
            InstanceId = entity.InstanceId,
            JobName = entity.JobName,
            Status = (JobStatus)entity.Status,
            StartedAt = FromUtc(entity.StartedAt),
            CompletedAt = FromUtc(entity.CompletedAt),
            DurationMilliseconds = entity.DurationMilliseconds,
            TenantId = entity.TenantId,
            TriggerType = (JobTriggerType)entity.TriggerType,
            IsSuccess = entity.IsSuccess,
            ErrorMessage = entity.ErrorMessage,
            StackTrace = entity.StackTrace,
            RetryCount = entity.RetryCount,
            ExecutionNode = entity.ExecutionNode,
            TraceId = entity.TraceId,
            ParametersJson = entity.ParametersJson,
            Remarks = entity.Remarks
        };
    }

    /// <summary>
    /// 计算运行中实例的截止时刻，非运行中状态返回空，不限时的任务返回 <see cref="UnboundedRunningDeadline"/>
    /// </summary>
    private static DateTime? ComputeRunningDeadline(JobInstance instance, TimeSpan runningGracePeriod)
    {
        if (instance.Status != JobStatus.Running)
        {
            return null;
        }

        var timeoutMilliseconds = instance.JobInfo?.TimeoutMilliseconds ?? 0;
        if (timeoutMilliseconds <= 0)
        {
            return UnboundedRunningDeadline;
        }

        var startedAt = instance.StartedAt ?? instance.ScheduledAt;

        return ToUtc(startedAt + TimeSpan.FromMilliseconds(timeoutMilliseconds) + runningGracePeriod);
    }

    /// <summary>
    /// 序列化执行参数，无法序列化时返回空
    /// </summary>
    private static string? SerializeParameters(IDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(parameters);
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 反序列化执行参数
    /// </summary>
    private static Dictionary<string, object?>? DeserializeParameters(string? parametersJson)
    {
        return string.IsNullOrWhiteSpace(parametersJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(parametersJson);
    }

    /// <summary>
    /// 按程序集限定名解析类型，解析不到时返回空
    /// </summary>
    private static Type? ResolveType(string? typeName)
    {
        return string.IsNullOrWhiteSpace(typeName)
            ? null
            : Type.GetType(typeName, throwOnError: false);
    }

    /// <summary>
    /// 转换为协调世界时
    /// </summary>
    private static DateTime ToUtc(DateTimeOffset value)
    {
        return value.UtcDateTime;
    }

    /// <summary>
    /// 转换为协调世界时
    /// </summary>
    private static DateTime? ToUtc(DateTimeOffset? value)
    {
        return value?.UtcDateTime;
    }

    /// <summary>
    /// 把协调世界时还原为偏移为零的时间
    /// </summary>
    private static DateTimeOffset FromUtc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    /// <summary>
    /// 把协调世界时还原为偏移为零的时间
    /// </summary>
    private static DateTimeOffset? FromUtc(DateTime? value)
    {
        return value.HasValue ? FromUtc(value.Value) : null;
    }
}

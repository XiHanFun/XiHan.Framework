// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using XiHan.Framework.Scheduling.Database.Entities;

namespace XiHan.Framework.Scheduling.Database.Storage;

/// <summary>
/// 契约记录与实体行之间的换算。
/// </summary>
/// <remarks>
/// 枚举存整数值，JSON 数据存文本，计划存 <see cref="JobSchedule.ToString"/> 的文本。<c>Copy</c> 把新内容复制到取回的行上，不碰标识、并发戳与写入序号——整体替换由协议维护的列保持在取回的实例里。
/// </remarks>
internal static class RecordMapping
{
    /// <summary>
    /// 作业记录 → 行。
    /// </summary>
    public static JobEntity ToEntity(JobRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var row = new JobEntity { Id = record.Name.Value };
        Copy(record, row);
        return row;
    }

    /// <summary>
    /// 作业行 → 记录。
    /// </summary>
    public static JobRecord ToRecord(JobEntity row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new JobRecord
        {
            Name = new JobName(row.Id),
            JobTypeName = row.JobTypeName,
            Data = ParseData(row.Data),
            AllowConcurrent = row.AllowConcurrent,
            RunOnEveryInstance = row.RunOnEveryInstance,
            RequestsRecovery = row.RequestsRecovery,
            Description = row.Description,
            Paused = row.Paused,
            ActiveRunId = row.ActiveRunId,
            Source = (JobSource)row.Source,
            UpdatedAt = row.UpdatedAt,
            CodeFingerprint = row.CodeFingerprint,
        };
    }

    /// <summary>
    /// 把新作业行的内容复制到已有行上。
    /// </summary>
    public static void Copy(JobEntity source, JobEntity target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        target.JobTypeName = source.JobTypeName;
        target.Data = source.Data;
        target.AllowConcurrent = source.AllowConcurrent;
        target.RunOnEveryInstance = source.RunOnEveryInstance;
        target.RequestsRecovery = source.RequestsRecovery;
        target.Description = source.Description;
        target.Paused = source.Paused;
        // 占用的运行 ID 由存储自己维护（独占开始 / 完成 / 失联恢复），替换定义不动它
        target.Source = source.Source;
        target.UpdatedAt = source.UpdatedAt;
        target.CodeFingerprint = source.CodeFingerprint;
    }

    /// <summary>
    /// 触发器记录 → 行。
    /// </summary>
    public static TriggerEntity ToEntity(TriggerRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var row = new TriggerEntity { Id = record.Key.ToString() };
        Copy(record, row);
        return row;
    }

    /// <summary>
    /// 触发器行 → 记录。
    /// </summary>
    public static TriggerRecord ToRecord(TriggerEntity row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new TriggerRecord
        {
            Job = new JobName(row.Job),
            Name = row.Name,
            Schedule = JobSchedule.Parse(row.Schedule),
            Data = ParseData(row.Data),
            Priority = row.Priority,
            MisfirePolicy = (MisfirePolicy)row.MisfirePolicy,
            Description = row.Description,
            State = (TriggerState)row.State,
            NextFireAt = row.NextFireAt,
            PreviousFireAt = row.PreviousFireAt,
            AcquiredBy = row.AcquiredBy,
            AcquiredAt = row.AcquiredAt,
            FireCount = row.FireCount,
            Error = row.Error,
            Local = row.Local,
            Transient = row.Transient,
            RunAs = (JobTrigger)row.RunAs,
        };
    }

    /// <summary>
    /// 把新触发器行的内容复制到已有行上。
    /// </summary>
    public static void Copy(TriggerEntity source, TriggerEntity target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        target.Job = source.Job;
        target.Name = source.Name;
        target.Schedule = source.Schedule;
        target.Data = source.Data;
        target.Priority = source.Priority;
        target.MisfirePolicy = source.MisfirePolicy;
        target.Description = source.Description;
        target.State = source.State;
        target.NextFireAt = source.NextFireAt;
        target.PreviousFireAt = source.PreviousFireAt;
        target.AcquiredBy = source.AcquiredBy;
        target.AcquiredAt = source.AcquiredAt;
        target.FireCount = source.FireCount;
        target.Error = source.Error;
        target.Local = source.Local;
        target.Transient = source.Transient;
        target.RunAs = source.RunAs;
    }

    /// <summary>
    /// 运行记录 → 行。
    /// </summary>
    /// <remarks>
    /// <paramref name="sequence"/> 只对新增的行生效，替换已有行时 <see cref="Copy(JobRunEntity, JobRunEntity)"/> 不复制它。
    /// </remarks>
    public static JobRunEntity ToEntity(JobRunRecord record, long sequence)
    {
        ArgumentNullException.ThrowIfNull(record);
        var row = new JobRunEntity { Id = record.Id, Sequence = sequence };
        Copy(record, row);
        return row;
    }

    /// <summary>
    /// 运行行 → 记录。
    /// </summary>
    public static JobRunRecord ToRecord(JobRunEntity row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new JobRunRecord
        {
            Id = row.Id,
            Job = new JobName(row.Job),
            TriggerName = row.TriggerName,
            Trigger = (JobTrigger)row.Trigger,
            ScheduledAt = row.ScheduledAt,
            StartedAt = row.StartedAt,
            CompletedAt = row.CompletedAt,
            State = (JobRunState)row.State,
            ErrorCode = row.ErrorCode,
            ErrorMessage = row.ErrorMessage,
            NodeId = row.NodeId,
            CancelRequested = row.CancelRequested,
            Data = ParseData(row.Data),
        };
    }

    /// <summary>
    /// 把新运行行的内容复制到已有行上。
    /// </summary>
    public static void Copy(JobRunEntity source, JobRunEntity target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        target.Job = source.Job;
        target.TriggerName = source.TriggerName;
        target.Trigger = source.Trigger;
        target.ScheduledAt = source.ScheduledAt;
        target.StartedAt = source.StartedAt;
        target.CompletedAt = source.CompletedAt;
        target.State = source.State;
        target.ErrorCode = source.ErrorCode;
        target.ErrorMessage = source.ErrorMessage;
        target.NodeId = source.NodeId;
        target.CancelRequested = source.CancelRequested;
        target.Data = source.Data;
    }

    /// <summary>
    /// 节点记录 → 行。
    /// </summary>
    public static SchedulerNodeEntity ToEntity(SchedulerNodeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new SchedulerNodeEntity { Id = record.Id, StartedAt = record.StartedAt, LastHeartbeatAt = record.LastHeartbeatAt };
    }

    /// <summary>
    /// 节点行 → 记录。
    /// </summary>
    public static SchedulerNodeRecord ToRecord(SchedulerNodeEntity row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new SchedulerNodeRecord { Id = row.Id, StartedAt = row.StartedAt, LastHeartbeatAt = row.LastHeartbeatAt };
    }

    /// <summary>
    /// 把新节点行的内容复制到已有行上。
    /// </summary>
    public static void Copy(SchedulerNodeEntity source, SchedulerNodeEntity target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        target.StartedAt = source.StartedAt;
        target.LastHeartbeatAt = source.LastHeartbeatAt;
    }

    /// <summary>
    /// 作业记录的字段写到行上（标识除外）。
    /// </summary>
    private static void Copy(JobRecord record, JobEntity row)
    {
        row.JobTypeName = record.JobTypeName;
        row.Data = record.Data.ToJsonString();
        row.AllowConcurrent = record.AllowConcurrent;
        row.RunOnEveryInstance = record.RunOnEveryInstance;
        row.RequestsRecovery = record.RequestsRecovery;
        row.Description = record.Description;
        row.Paused = record.Paused;
        row.ActiveRunId = record.ActiveRunId;
        row.Source = (int)record.Source;
        row.UpdatedAt = record.UpdatedAt;
        row.CodeFingerprint = record.CodeFingerprint;
    }

    /// <summary>
    /// 触发器记录的字段写到行上（标识除外）。
    /// </summary>
    private static void Copy(TriggerRecord record, TriggerEntity row)
    {
        row.Job = record.Job.Value;
        row.Name = record.Name;
        row.Schedule = record.Schedule.ToString();
        row.Data = record.Data.ToJsonString();
        row.Priority = record.Priority;
        row.MisfirePolicy = (int)record.MisfirePolicy;
        row.Description = record.Description;
        row.State = (int)record.State;
        row.NextFireAt = record.NextFireAt;
        row.PreviousFireAt = record.PreviousFireAt;
        row.AcquiredBy = record.AcquiredBy;
        row.AcquiredAt = record.AcquiredAt;
        row.FireCount = record.FireCount;
        row.Error = record.Error;
        row.Local = record.Local;
        row.Transient = record.Transient;
        row.RunAs = (int)record.RunAs;
    }

    /// <summary>
    /// 运行记录的字段写到行上（标识与序号除外）。
    /// </summary>
    private static void Copy(JobRunRecord record, JobRunEntity row)
    {
        row.Job = record.Job.Value;
        row.TriggerName = record.TriggerName;
        row.Trigger = (int)record.Trigger;
        row.ScheduledAt = record.ScheduledAt;
        row.StartedAt = record.StartedAt;
        row.CompletedAt = record.CompletedAt;
        row.State = (int)record.State;
        row.ErrorCode = record.ErrorCode;
        row.ErrorMessage = record.ErrorMessage;
        row.NodeId = record.NodeId;
        row.CancelRequested = record.CancelRequested;
        row.Data = record.Data.ToJsonString();
    }

    /// <summary>
    /// JSON 文本 → 对象。
    /// </summary>
    /// <remarks>
    /// 空文本或不是对象的文本按空对象处理。
    /// </remarks>
    private static JsonObject ParseData(string text)
        => string.IsNullOrWhiteSpace(text) ? [] : JsonNode.Parse(text) as JsonObject ?? [];
}

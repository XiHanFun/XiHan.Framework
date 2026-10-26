// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 运行记录（历史）：存储里的可变形态。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobRunRecord
{
    /// <summary>
    /// 运行 ID。
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 作业名。
    /// </summary>
    public required JobName Job { get; set; }

    /// <summary>
    /// 触发器名；手动触发为空。
    /// </summary>
    public string? TriggerName { get; set; }

    /// <summary>
    /// 触发来源。
    /// </summary>
    public JobTrigger Trigger { get; set; }

    /// <summary>
    /// 计划的运行时刻。
    /// </summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// 开始时刻。
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// 结束时刻；运行中为空。
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// 状态。
    /// </summary>
    public JobRunState State { get; set; }

    /// <summary>
    /// 失败的错误码。
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 失败的错误消息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 运行所在节点。
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// 是否已请求取消。
    /// </summary>
    public bool CancelRequested { get; set; }

    /// <summary>
    /// 传给作业的数据。
    /// </summary>
    public JsonObject Data { get; set; } = [];

    /// <summary>
    /// 还原为运行。
    /// </summary>
    public JobRun ToRun() => new(Id, Job, TriggerName, ScheduledAt, Trigger, (JsonObject)Data.DeepClone());

    /// <summary>
    /// 还原为结果：成功，或按错误码与消息还原的失败（文案按原样还原，不再翻译）。
    /// </summary>
    public Result ToResult() => State == JobRunState.Succeeded || ErrorCode is null
        ? Result.Success()
        : Result.Failure(Error.Literal(ErrorCode, ErrorMessage ?? ErrorCode));

    /// <summary>
    /// 还原为运行结果；运行中为空。
    /// </summary>
    public JobOutcome? ToOutcome() => CompletedAt is { } completed ? new JobOutcome(ToRun(), StartedAt, completed, ToResult()) : null;

    /// <summary>
    /// 深拷贝。
    /// </summary>
    public JobRunRecord Clone() => new()
    {
        Id = Id,
        Job = Job,
        TriggerName = TriggerName,
        Trigger = Trigger,
        ScheduledAt = ScheduledAt,
        StartedAt = StartedAt,
        CompletedAt = CompletedAt,
        State = State,
        ErrorCode = ErrorCode,
        ErrorMessage = ErrorMessage,
        NodeId = NodeId,
        CancelRequested = CancelRequested,
        Data = (JsonObject)Data.DeepClone(),
    };
}

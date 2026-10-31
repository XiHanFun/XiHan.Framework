// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Entities;

/// <summary>
/// 运行记录行。
/// </summary>
/// <remarks>
/// <c>Id</c> 是运行 ID，状态与触发来源存枚举的整数值，数据是 JSON 文本；同刻排序以写入序号 <see cref="Sequence"/> 为次关键字。没有并发戳：运行记录只由它所在的节点写回，取消请求只改一个标记，后写覆盖先写即可。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobRunEntity : IEntity<string>
{
    /// <inheritdoc />
    [StringLength(128)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 作业名。
    /// </summary>
    [StringLength(128)]
    public string Job { get; set; } = string.Empty;

    /// <summary>
    /// 触发器名。
    /// </summary>
    /// <remarks>
    /// 手动触发为空。
    /// </remarks>
    [StringLength(256)]
    public string? TriggerName { get; set; }

    /// <summary>
    /// 触发来源（<c>JobTrigger</c> 的整数值）。
    /// </summary>
    public int Trigger { get; set; }

    /// <summary>
    /// 计划的运行时刻。
    /// </summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// 开始时刻。
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// 结束时刻。
    /// </summary>
    /// <remarks>
    /// 运行中为空，已结束的行总有值。
    /// </remarks>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// 状态（<c>JobRunState</c> 的整数值）。
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// 失败的错误码。
    /// </summary>
    [StringLength(64)]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 失败的错误消息。
    /// </summary>
    [LongText]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 运行所在节点。
    /// </summary>
    [StringLength(256)]
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 是否已请求取消。
    /// </summary>
    public bool CancelRequested { get; set; }

    /// <summary>
    /// 传给作业的数据，JSON 对象文本。
    /// </summary>
    /// <remarks>
    /// 长文本列。
    /// </remarks>
    [LongText]
    public string Data { get; set; } = "{}";

    /// <summary>
    /// 第几次尝试。
    /// </summary>
    /// <remarks>
    /// 空按 1（ADR-0060 之前写入的行）。
    /// </remarks>
    public int? Attempt { get; set; }

    /// <summary>
    /// 重试时为首次运行的 ID。
    /// </summary>
    [StringLength(128)]
    public string? RetryOf { get; set; }

    /// <summary>
    /// 写入序号。
    /// </summary>
    /// <remarks>
    /// 同刻排序的次关键字，首次写入时由存储分配，整体替换保留。UUID v7 只到毫秒单调，运行 ID 不能当写入顺序用。
    /// </remarks>
    public long Sequence { get; set; }
}

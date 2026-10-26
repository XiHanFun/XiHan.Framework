// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Entities;

/// <summary>
/// 作业行：<c>Id</c> 是作业名，<c>JobRecord</c> 的其余字段逐列存放，数据是 JSON 文本，来源存枚举的整数值。
/// 并发戳让独占运行对 <see cref="ActiveRunId"/> 的占用与释放在跨进程下是原子的比较并交换。不实现 <c>IMultiTenant</c>，存储不按租户过滤。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JobEntity : IEntity<string>, IHasConcurrencyStamp
{
    /// <inheritdoc />
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 作业类型键。
    /// </summary>
    public string JobTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 运行时传给作业的数据，JSON 对象文本；长文本列。
    /// </summary>
    public string Data { get; set; } = "{}";

    /// <summary>
    /// 允许多次运行重叠。
    /// </summary>
    public bool AllowConcurrent { get; set; }

    /// <summary>
    /// 每个实例各自运行。
    /// </summary>
    public bool RunOnEveryInstance { get; set; }

    /// <summary>
    /// 节点失联时重新入队。
    /// </summary>
    public bool RequestsRecovery { get; set; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否暂停。
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>
    /// 不允许并发时正在运行的运行 ID；没有为空。
    /// </summary>
    public string? ActiveRunId { get; set; }

    /// <summary>
    /// 来源（<c>JobSource</c> 的整数值）。
    /// </summary>
    public int Source { get; set; }

    /// <summary>
    /// 最近写入时刻。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <inheritdoc />
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

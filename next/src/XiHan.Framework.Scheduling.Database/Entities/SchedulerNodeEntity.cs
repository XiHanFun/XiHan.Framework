// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Entities;

/// <summary>
/// 调度节点行。
/// </summary>
/// <remarks>
/// <c>Id</c> 是节点 ID，每个运行引擎的实例按周期写心跳，领导者据此发现失联节点并恢复。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SchedulerNodeEntity : IEntity<string>
{
    /// <inheritdoc />
    [StringLength(256)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 启动时刻。
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// 最近心跳时刻。
    /// </summary>
    public DateTimeOffset LastHeartbeatAt { get; set; }
}

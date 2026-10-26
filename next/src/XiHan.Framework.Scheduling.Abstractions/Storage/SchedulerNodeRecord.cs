// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Storage;

/// <summary>
/// 调度节点记录：集群里每个运行引擎的实例按周期写心跳，领导者据此发现失联节点。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SchedulerNodeRecord
{
    /// <summary>
    /// 节点 ID。
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 启动时刻。
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// 最近心跳时刻。
    /// </summary>
    public DateTimeOffset LastHeartbeatAt { get; set; }

    /// <summary>
    /// 深拷贝。
    /// </summary>
    public SchedulerNodeRecord Clone() => new() { Id = Id, StartedAt = StartedAt, LastHeartbeatAt = LastHeartbeatAt };
}

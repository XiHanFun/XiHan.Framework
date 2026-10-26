// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 外部任务视图（领取时返回）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ExternalTask
{
    /// <summary>
    /// 任务 ID（= 书签 ID）。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 实例 ID。
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 主题。
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// 活动声明的载荷。
    /// </summary>
    public JsonObject? Payload { get; init; }

    /// <summary>
    /// 实例当前变量（快照）。
    /// </summary>
    public JsonObject? Variables { get; init; }

    /// <summary>
    /// 锁到期时刻。
    /// </summary>
    public DateTimeOffset LockedUntil { get; init; }

    /// <summary>
    /// 第几次尝试（从 1 起，每次带 <c>RetryAfter</c> 的失败加一）。
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// 关联 ID。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 租户。
    /// </summary>
    public string? TenantId { get; init; }
}

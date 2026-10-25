// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 挂起点：节点在等什么（种类与键），到期时间与载荷；被消费（删除）时恢复节点。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowBookmark
{
    /// <summary>
    /// 书签 ID；用户任务的任务 ID就是它。
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 所属实例 ID。
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// 节点实例 ID。
    /// </summary>
    public required string NodeInstanceId { get; set; }

    /// <summary>
    /// 种类（<see cref="BookmarkKinds"/>）。
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>
    /// 键：用户任务是指派人，信号是信号名，子流程是父节点实例 ID。
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 载荷。
    /// </summary>
    public JsonObject Payload { get; set; } = [];

    /// <summary>
    /// 到期时间（定时、重试、超时书签）。
    /// </summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>
    /// 关联 ID（信号书签限定关联）。
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 租户（随实例）。
    /// </summary>
    public string? TenantId { get; set; }
}

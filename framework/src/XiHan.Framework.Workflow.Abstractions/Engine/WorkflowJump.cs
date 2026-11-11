// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 跳转请求：取消未结束的节点与书签后从目标节点重新执行
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowJump
{
    /// <summary>
    /// 创建跳转请求
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    /// <param name="targetNodeId">目标节点 ID</param>
    public WorkflowJump(string instanceId, string targetNodeId)
    {
        InstanceId = instanceId;
        TargetNodeId = targetNodeId;
    }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// 目标节点 ID
    /// </summary>
    public string TargetNodeId { get; }

    /// <summary>
    /// 原因（写进事件）
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 跳转前合并进实例的变量
    /// </summary>
    public JsonObject? Variables { get; init; }

    /// <summary>
    /// 操作者（写进事件）
    /// </summary>
    public string? ActorId { get; init; }
}

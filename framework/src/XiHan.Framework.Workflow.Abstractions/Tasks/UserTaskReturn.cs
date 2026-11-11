// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 退回请求：从本任务所在节点跳回上游节点
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskReturn
{
    /// <summary>
    /// 创建退回请求
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="actorId">操作者（必须是指派人）</param>
    public UserTaskReturn(string taskId, string actorId)
    {
        TaskId = taskId;
        ActorId = actorId;
    }

    /// <summary>
    /// 任务 ID
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 操作者（必须是指派人）
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// 目标节点
    /// </summary>
    /// <remarks>
    /// 空为本节点之前最近完成的 <c>UserTask</c> 节点，没有则 <c>Start</c> 之后的首个节点
    /// </remarks>
    public string? TargetNodeId { get; init; }

    /// <summary>
    /// 意见（写进节点历史与事件）
    /// </summary>
    public string? Comment { get; init; }
}

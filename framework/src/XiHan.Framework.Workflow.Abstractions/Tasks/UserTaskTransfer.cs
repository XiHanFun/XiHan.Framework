// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 转办一个用户任务：把任务交给另一个指派人
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskTransfer
{
    /// <summary>
    /// 创建转办一个用户任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="actorId">操作者（当前指派人）</param>
    public UserTaskTransfer(string taskId, string actorId)
    {
        TaskId = taskId;
        ActorId = actorId;
    }

    /// <summary>
    /// 任务 ID
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 操作者（当前指派人）
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// 目标指派人
    /// </summary>
    public required string TargetAssigneeId { get; init; }

    /// <summary>
    /// 意见
    /// </summary>
    public string? Comment { get; init; }
}

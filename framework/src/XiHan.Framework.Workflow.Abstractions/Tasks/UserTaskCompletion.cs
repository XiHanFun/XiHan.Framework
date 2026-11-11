// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 完成一个用户任务：操作者必须是任务的指派人
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskCompletion
{
    /// <summary>
    /// 创建完成一个用户任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="actorId">操作者</param>
    public UserTaskCompletion(string taskId, string actorId)
    {
        TaskId = taskId;
        ActorId = actorId;
    }

    /// <summary>
    /// 任务 ID
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 操作者
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// 结果（须在节点允许范围内）
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// 意见
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// 随任务写入实例的变量
    /// </summary>
    public JsonObject? Variables { get; init; }
}

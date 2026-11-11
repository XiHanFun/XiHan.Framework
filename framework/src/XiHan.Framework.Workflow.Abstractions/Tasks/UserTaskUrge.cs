// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 催办请求：发布 <c>workflow.task.urged</c> 事件
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskUrge
{
    /// <summary>
    /// 创建催办请求
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="actorId">催办人</param>
    public UserTaskUrge(string taskId, string actorId)
    {
        TaskId = taskId;
        ActorId = actorId;
    }

    /// <summary>
    /// 任务 ID
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 催办人
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// 留言
    /// </summary>
    public string? Comment { get; init; }
}

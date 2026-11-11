// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 释放认领的请求
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskRelease
{
    /// <summary>
    /// 创建释放认领的请求
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="actorId">操作者（必须是认领人）</param>
    public UserTaskRelease(string taskId, string actorId)
    {
        TaskId = taskId;
        ActorId = actorId;
    }

    /// <summary>
    /// 任务 ID
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 操作者（必须是认领人）
    /// </summary>
    public string ActorId { get; }
}

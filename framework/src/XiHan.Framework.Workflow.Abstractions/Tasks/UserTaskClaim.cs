// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 认领请求（<c>Any</c> 策略下由一个指派人独占任务）
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskClaim
{
    /// <summary>
    /// 创建认领请求（<c>Any</c> 策略下由一个指派人独占任务）
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="actorId">操作者（必须是指派人）</param>
    public UserTaskClaim(string taskId, string actorId)
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
}

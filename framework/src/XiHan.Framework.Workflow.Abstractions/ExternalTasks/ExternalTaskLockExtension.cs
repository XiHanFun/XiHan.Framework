// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 延长外部任务的锁
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ExternalTaskLockExtension
{
    /// <summary>
    /// 创建延长外部任务的锁
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="workerId">工作者（必须持有锁）</param>
    public ExternalTaskLockExtension(string taskId, string workerId)
    {
        TaskId = taskId;
        WorkerId = workerId;
    }

    /// <summary>
    /// 任务 ID
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 工作者（必须持有锁）
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// 从现在起的新锁时长
    /// </summary>
    public required TimeSpan LockDuration { get; init; }
}

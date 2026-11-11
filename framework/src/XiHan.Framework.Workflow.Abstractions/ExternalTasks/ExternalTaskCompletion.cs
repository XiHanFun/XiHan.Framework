// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.ExternalTasks;

/// <summary>
/// 外部任务完成
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ExternalTaskCompletion
{
    /// <summary>
    /// 创建外部任务完成
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="workerId">工作者（必须持有锁）</param>
    public ExternalTaskCompletion(string taskId, string workerId)
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
    /// 输出，合并进实例变量
    /// </summary>
    public JsonObject? Outputs { get; init; }
}

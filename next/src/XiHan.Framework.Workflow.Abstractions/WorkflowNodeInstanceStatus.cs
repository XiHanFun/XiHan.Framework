// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 节点实例的状态。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowNodeInstanceStatus>))]
public enum WorkflowNodeInstanceStatus
{
    /// <summary>执行中。</summary>
    Running = 1,

    /// <summary>已挂起，等待书签。</summary>
    Suspended = 2,

    /// <summary>已完成。</summary>
    Completed = 3,

    /// <summary>已取消（实例被取消或终止时清理）。</summary>
    Canceled = 4,

    /// <summary>已故障（等待重试、继续或实例故障）。</summary>
    Faulted = 5,

    /// <summary>已补偿。</summary>
    Compensated = 6,
}

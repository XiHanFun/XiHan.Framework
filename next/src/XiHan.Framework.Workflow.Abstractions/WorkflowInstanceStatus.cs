// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 实例的状态；<see cref="Completed"/>、<see cref="Canceled"/>、<see cref="Faulted"/>、<see cref="Terminated"/> 是终态（故障可重试）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowInstanceStatus>))]
public enum WorkflowInstanceStatus
{
    /// <summary>运行中（含等待书签）。</summary>
    Running = 1,

    /// <summary>已挂起：书签保留，到期的书签不恢复。</summary>
    Suspended = 2,

    /// <summary>已完成。</summary>
    Completed = 3,

    /// <summary>已取消（可能已补偿）。</summary>
    Canceled = 4,

    /// <summary>已故障；有故障节点时可重试。</summary>
    Faulted = 5,

    /// <summary>已终止。</summary>
    Terminated = 6,
}

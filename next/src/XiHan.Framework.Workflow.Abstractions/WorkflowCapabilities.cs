// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Engine;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 工作流子系统的能力 ID（RFC-0021）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowCapabilities
{
    /// <summary>持久化执行：定义目录、实例执行、书签、用户任务，<see cref="IWorkflowEngine"/>。</summary>
    public const string Execution = "workflow.execution";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}

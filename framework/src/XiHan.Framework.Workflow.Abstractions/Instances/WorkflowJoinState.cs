// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 汇合节点的到达记录：已到达的转移 ID 与本轮是否已触发
/// </summary>
internal sealed class WorkflowJoinState
{
    /// <summary>
    /// 已到达的转移 ID
    /// </summary>
    public List<string> Arrived { get; set; } = [];

    /// <summary>
    /// 本轮是否已触发（<c>WaitAny</c> 首个到达即触发）
    /// </summary>
    public bool Fired { get; set; }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 改实例变量的请求
/// </summary>
/// <remarks>
/// 以后加字段只加初始化器属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowVariableUpdate
{
    /// <summary>
    /// 创建改实例变量的请求
    /// </summary>
    /// <param name="instanceId">实例 ID</param>
    public WorkflowVariableUpdate(string instanceId)
    {
        InstanceId = instanceId;
    }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// 要合并（新增或覆盖）的变量
    /// </summary>
    public JsonObject? Variables { get; init; }

    /// <summary>
    /// 要删除的变量名
    /// </summary>
    public IReadOnlyList<string>? Remove { get; init; }
}

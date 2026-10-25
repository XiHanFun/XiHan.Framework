// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 启动实例的请求：按定义 ID 或名字（加可选版本，缺省最新已发布）找定义。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowStartRequest
{
    /// <summary>
    /// 定义 ID；优先于名字。
    /// </summary>
    public string? DefinitionId { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public WorkflowName? Name { get; init; }

    /// <summary>
    /// 定义版本；缺省最新已发布版本。
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    /// 实例显示名；缺省定义的显示名。
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 初始变量。
    /// </summary>
    public JsonObject? Variables { get; init; }

    /// <summary>
    /// 业务关联 ID。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public string? StarterId { get; init; }

    /// <summary>
    /// 租户；缺省取当前调用的租户。
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// 指定实例 ID；缺省由引擎生成。子流程由引擎设置。
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// 父实例 ID；子流程由引擎设置。
    /// </summary>
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// 父节点实例 ID；子流程由引擎设置。
    /// </summary>
    public string? ParentNodeInstanceId { get; init; }

    /// <summary>
    /// 子流程深度；顶层 0，子流程由引擎设置。
    /// </summary>
    public int Depth { get; init; }
}

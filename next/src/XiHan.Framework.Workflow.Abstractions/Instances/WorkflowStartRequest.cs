// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 启动请求：按定义 ID 或名字（可带版本，缺省最新已发布）启动。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowStartRequest
{
    /// <summary>
    /// 定义 ID。
    /// </summary>
    /// <remarks>
    /// 给出时忽略名字与版本。
    /// </remarks>
    public string? DefinitionId { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public WorkflowName? Name { get; init; }

    /// <summary>
    /// 版本。
    /// </summary>
    /// <remarks>
    /// 空为最新已发布版本。
    /// </remarks>
    public int? Version { get; init; }

    /// <summary>
    /// 实例显示名。
    /// </summary>
    /// <remarks>
    /// 空取定义的。
    /// </remarks>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 初始变量。
    /// </summary>
    public JsonObject? Variables { get; init; }

    /// <summary>
    /// 关联 ID（业务键）。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public string? StarterId { get; init; }

    /// <summary>
    /// 租户。
    /// </summary>
    /// <remarks>
    /// 空取当前调用的租户。
    /// </remarks>
    public string? TenantId { get; init; }

    /// <summary>
    /// 指定实例 ID（子流程用）。
    /// </summary>
    /// <remarks>
    /// 空由引擎生成。
    /// </remarks>
    public string? InstanceId { get; init; }

    /// <summary>
    /// 父实例 ID（子流程用）。
    /// </summary>
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// 父节点实例 ID（子流程用）。
    /// </summary>
    public string? ParentNodeInstanceId { get; init; }

    /// <summary>
    /// 嵌套深度（子流程用）。
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// 延迟启动。
    /// </summary>
    /// <remarks>
    /// 实例创建后在该时刻才执行 <c>Start</c> 之后的节点；空或已过去则立即执行。
    /// </remarks>
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>
    /// 同一定义名与关联 ID 下已有未结束实例时的策略。
    /// </summary>
    public WorkflowReusePolicy ReusePolicy { get; init; }
}

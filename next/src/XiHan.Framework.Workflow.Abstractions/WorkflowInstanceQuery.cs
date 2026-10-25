// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 实例列表查询：过滤项都可空；结果按创建时间降序，最多 <see cref="Limit"/> 条。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowInstanceQuery
{
    /// <summary>
    /// 上限的最大值。
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// 状态。
    /// </summary>
    public WorkflowInstanceStatus? Status { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public WorkflowName? DefinitionName { get; init; }

    /// <summary>
    /// 关联 ID。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 父实例 ID。
    /// </summary>
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// 租户。
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// 最多返回条数，1 到 <see cref="MaxLimit"/>；默认 100。
    /// </summary>
    public int Limit
    {
        get => field;
        init => field = value is >= 1 and <= MaxLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The limit must be between 1 and {MaxLimit}.");
    } = 100;
}

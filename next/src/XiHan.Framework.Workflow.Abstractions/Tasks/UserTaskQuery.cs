// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 待办查询：<see cref="AssigneeId"/> 与 <see cref="InstanceId"/> 必填其一；其余过滤项可空、同时给出的取交集；按创建时间升序，跳过 <see cref="Skip"/> 条后最多 <see cref="Limit"/> 条。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskQuery
{
    /// <summary>
    /// 指派人。
    /// </summary>
    public string? AssigneeId { get; init; }

    /// <summary>
    /// 实例 ID。
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public WorkflowName? DefinitionName { get; init; }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 创建时间下界（含）。
    /// </summary>
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>
    /// 创建时间上界（不含）。
    /// </summary>
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>
    /// 跳过条数；默认 0。
    /// </summary>
    public int Skip
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The skip must not be negative.");
    }

    /// <summary>
    /// 最多返回条数，1 到 <see cref="WorkflowInstanceQuery.MaxLimit"/>；默认 100。
    /// </summary>
    public int Limit
    {
        get => field;
        init => field = value is >= 1 and <= WorkflowInstanceQuery.MaxLimit ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"The limit must be between 1 and {WorkflowInstanceQuery.MaxLimit}.");
    } = 100;
}

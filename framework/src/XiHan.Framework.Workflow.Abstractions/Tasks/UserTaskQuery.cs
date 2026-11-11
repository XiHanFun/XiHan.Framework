// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 待办查询
/// </summary>
/// <remarks>
/// <see cref="AssigneeId"/> 与 <see cref="InstanceId"/> 必填其一；其余过滤项可空、同时给出的取交集；按创建时间排序（<see cref="Sorts"/>），跳过 <see cref="Skip"/> 条后最多 <see cref="Take"/> 条。过滤、排序与分页都由存储执行（ADR-0077）。分页越界不在构造时抛出，由 <see cref="IWorkflowTaskService.QueryAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>（HTTP 400）：查询常由请求绑定而来，越界是调用方可以改正的输入
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskQuery
{
    /// <summary>
    /// 指派人
    /// </summary>
    public string? AssigneeId { get; init; }

    /// <summary>
    /// 实例 ID
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// 定义名
    /// </summary>
    public WorkflowName? DefinitionName { get; init; }

    /// <summary>
    /// 节点 ID
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 关键字
    /// </summary>
    /// <remarks>
    /// 不区分大小写地包含在任务标题（<see cref="UserTask.Title"/>：节点 <c>Title</c> 模板渲染的结果，缺省节点名）、所属实例的标题（<see cref="UserTask.InstanceTitle"/>）、定义名（<see cref="UserTask.DefinitionName"/>）或关联 ID（所属实例的 <see cref="Instances.WorkflowInstance.CorrelationId"/>；用户任务书签的关联 ID 就取自所属实例，与 <see cref="UserTask.CorrelationId"/> 相同）任一里即匹配，规则见 <see cref="WorkflowKeyword"/>；为 <see langword="null"/>、空或只有空白时不过滤（ADR-0096、ADR-0099）
    /// </remarks>
    public string? Keyword { get; init; }

    /// <summary>
    /// 创建时间下界（含）
    /// </summary>
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>
    /// 创建时间上界（不含）
    /// </summary>
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>
    /// 页大小的上限
    /// </summary>
    /// <remarks>
    /// 与持久化的查询条数上限（<c>EntityQuery.DefaultMaxTake</c>）一致
    /// </remarks>
    public const int MaxTake = 1000;

    /// <summary>
    /// 排序项最多的个数
    /// </summary>
    public const int MaxSorts = 1;

    /// <summary>
    /// 排序项
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 或空列表时按创建时间升序（先到先办）；同刻按写入先后。项数超过 <see cref="MaxSorts"/>（<c>workflow.task.query_sorts_too_many</c>）、某项为空或字段不是 <see cref="UserTaskSortField"/> 里定义的值（<c>workflow.task.query_sort_invalid</c>）时 <see cref="IWorkflowTaskService.QueryAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>（ADR-0162）
    /// </remarks>
    public IReadOnlyList<UserTaskSort>? Sorts { get; init; }

    /// <summary>
    /// 跳过条数
    /// </summary>
    /// <remarks>
    /// 默认 0，不能为负
    /// </remarks>
    public int Skip { get; init; }

    /// <summary>
    /// 最多返回条数，1 到 <see cref="MaxTake"/>
    /// </summary>
    /// <remarks>
    /// 默认 100
    /// </remarks>
    public int Take { get; init; } = 100;
}

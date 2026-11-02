// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 实例列表可以按哪个字段排序。
/// </summary>
/// <remarks>
/// 白名单，以 4.x 实例列表能排的列为参照（ADR-0102）；不在其中的值（如请求里给了越界的数字）由 <see cref="Engine.IWorkflowEngine.ListInstancesAsync"/> 返回 <c>WORKFLOW_VALIDATION_002</c>。排序字段之后接次序键创建时间降序、写入先后降序，规则见 <see cref="WorkflowInstanceQuery.SortBy"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[JsonConverter(typeof(JsonStringEnumConverter<WorkflowInstanceSortField>))]
public enum WorkflowInstanceSortField
{
    /// <summary>
    /// 创建时间。
    /// </summary>
    CreatedAt = 0,

    /// <summary>
    /// 结束时间。
    /// </summary>
    /// <remarks>
    /// 没结束的为空：空值的位置照数据库，内存实现当作最小。
    /// </remarks>
    EndedAt = 1,

    /// <summary>
    /// 状态。
    /// </summary>
    /// <remarks>
    /// 按生命周期的先后：<see cref="WorkflowInstanceStatus.Running"/> → <see cref="WorkflowInstanceStatus.Suspended"/> → <see cref="WorkflowInstanceStatus.Completed"/> → <see cref="WorkflowInstanceStatus.Canceled"/> → <see cref="WorkflowInstanceStatus.Faulted"/> → <see cref="WorkflowInstanceStatus.Terminated"/>，同 4.x（ADR-0103）。
    /// </remarks>
    Status = 2,

    /// <summary>
    /// 定义名（4.x 的定义编码）。
    /// </summary>
    DefinitionName = 3,

    /// <summary>
    /// 实例标题（4.x 的实例名称）。
    /// </summary>
    /// <remarks>
    /// 按前 <see cref="WorkflowSortKeys.MaxTitleLength"/> 个字符比较（<see cref="WorkflowSortKeys.Title"/>）。
    /// </remarks>
    Title = 4,
}

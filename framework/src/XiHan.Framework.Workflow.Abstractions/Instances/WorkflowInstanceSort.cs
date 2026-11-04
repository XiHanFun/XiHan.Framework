// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 实例列表的一个排序项：字段加方向
/// </summary>
/// <remarks>
/// 放进 <see cref="WorkflowInstanceQuery.Sorts"/>，列表的顺序就是排序的先后（第一项是主排序字段，ADR-0104）。方向缺省升序：要最新的在前，写 <c>new(WorkflowInstanceSortField.CreatedAt, Descending: true)</c>
/// </remarks>
/// <param name="Field">排序字段，白名单见 <see cref="WorkflowInstanceSortField"/></param>
/// <param name="Descending">是否降序，缺省升序</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowInstanceSort(WorkflowInstanceSortField Field, bool Descending = false);

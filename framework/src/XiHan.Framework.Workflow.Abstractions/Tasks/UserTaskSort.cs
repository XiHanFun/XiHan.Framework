// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 待办列表的一个排序项
/// </summary>
/// <remarks>
/// 放进 <see cref="UserTaskQuery.Sorts"/>，列表的顺序就是排序的先后，与定义、实例查询的排序项同一写法（ADR-0162）
/// </remarks>
/// <param name="Field">排序字段</param>
/// <param name="Descending">是否降序，缺省升序</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskSort(UserTaskSortField Field, bool Descending = false);

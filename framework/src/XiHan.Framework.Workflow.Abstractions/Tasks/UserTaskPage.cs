// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 待办查询的一页结果与总数
/// </summary>
/// <remarks>
/// 与框架其他分页结果同形（<c>Items</c> 是这一页的条目）；<see cref="Total"/> 是同条件、不分页的总数，供调用方算页数
/// </remarks>
/// <param name="Items">这一页的任务，按查询的排序</param>
/// <param name="Total">同条件的任务总数</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskPage(IReadOnlyList<UserTask> Items, long Total);

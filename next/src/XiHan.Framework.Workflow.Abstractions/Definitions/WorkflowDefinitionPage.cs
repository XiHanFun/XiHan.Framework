// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义列表的一页摘要与总数。
/// </summary>
/// <remarks>
/// 与待办查询的 <c>UserTaskPage</c> 同形；<see cref="Total"/> 是同条件、不分页的总数，供调用方算页数。
/// </remarks>
/// <param name="Items">这一页的摘要，按名字升序、版本降序。</param>
/// <param name="Total">同条件的定义总数。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowDefinitionPage(IReadOnlyList<WorkflowDefinitionSummary> Items, long Total);

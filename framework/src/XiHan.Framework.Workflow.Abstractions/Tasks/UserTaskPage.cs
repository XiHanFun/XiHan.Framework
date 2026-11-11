// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 待办查询的一页结果与总数
/// </summary>
/// <remarks>
/// 与框架其他分页结果同形（<c>Items</c> 是这一页的条目）；<see cref="Total"/> 是同条件、不分页的总数，供调用方算页数。以后加字段只加初始化器属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record UserTaskPage
{
    /// <summary>
    /// 这一页的任务，按查询的排序
    /// </summary>
    public required IReadOnlyList<UserTask> Items { get; init; }

    /// <summary>
    /// 同条件的任务总数
    /// </summary>
    public required long Total { get; init; }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 待办列表可排序的字段
/// </summary>
/// <remarks>
/// 整数值是持久化与线上契约，以后加字段只追加新值（ADR-0162）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum UserTaskSortField
{
    /// <summary>
    /// 创建时间
    /// </summary>
    CreatedAt = 0,
}

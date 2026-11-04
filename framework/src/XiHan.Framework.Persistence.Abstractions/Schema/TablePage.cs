// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 一页表
/// </summary>
/// <param name="Items">本页的表（不含列）</param>
/// <param name="Total">过滤后的总数（不受分页影响）</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TablePage(IReadOnlyList<TableInfo> Items, int Total);

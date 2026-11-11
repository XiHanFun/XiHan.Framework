// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 表列表的一页
/// </summary>
/// <remarks>
/// 成员经初始化器给出，与其他偏移分页同名、总数是 <see langword="long"/>（ADR-0154）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TablePage
{
    /// <summary>
    /// 本页的表
    /// </summary>
    public required IReadOnlyList<TableInfo> Items { get; init; }

    /// <summary>
    /// 过滤后的总数
    /// </summary>
    /// <remarks>
    /// 不受分页影响
    /// </remarks>
    public required long Total { get; init; }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 列表的过滤与分页。
/// </summary>
/// <remarks>
/// 取出条数与仓储查询同一上限：<see cref="Take"/> 取 1 到 <see cref="EntityQuery.DefaultMaxTake"/>，缺省取上限。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TableListQuery
{
    /// <summary>
    /// 关键字：在表名与注释里不区分大小写地包含匹配。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 或空白不过滤。
    /// </remarks>
    public string? Keyword { get; init; }

    /// <summary>
    /// 跳过的条数（不小于 0）。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">为负。</exception>
    public int Skip
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'Skip' must not be negative.");
    }

    /// <summary>
    /// 取出的条数（1 到 <see cref="EntityQuery.DefaultMaxTake"/>，缺省取上限）。
    /// </summary>
    /// <remarks>
    /// 大于上限抛出携带 <see cref="PersistenceErrors.TakeExceeded"/> 的 <see cref="FrameworkException"/>，调用里由 Transaction 阶段转为同码的校验结果（HTTP 400）；小于 1 是编程错误，抛出 <see cref="ArgumentOutOfRangeException"/>。不静默截断，要更多就分页。
    /// </remarks>
    public int Take
    {
        get => field;
        init => field = value switch
        {
            < 1 => throw new ArgumentOutOfRangeException(nameof(value), value, "'Take' must be at least 1."),
            > EntityQuery.DefaultMaxTake => throw new TakeExceededException(value),
            _ => value,
        };
    } = EntityQuery.DefaultMaxTake;
}

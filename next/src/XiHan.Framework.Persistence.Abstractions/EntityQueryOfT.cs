// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Domain;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 实体查询：过滤、排序、分页。不可变，用 <c>with</c> 或链式方法派生。
/// </summary>
/// <typeparam name="T">实体类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record EntityQuery<T>
{
    /// <summary>
    /// 过滤规约；<see langword="null"/> 为不过滤。
    /// </summary>
    public ISpecification<T>? Filter { get; init; }

    /// <summary>
    /// 排序，按顺序应用。
    /// </summary>
    public IReadOnlyList<Ordering<T>> Orderings { get; init; } = [];

    /// <summary>
    /// 跳过条数。
    /// </summary>
    public int Skip
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'Skip' must not be negative.");
    }

    /// <summary>
    /// 取出条数上限；Provider 不得返回超过此数的结果。
    /// </summary>
    public int Take
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'Take' must be positive.");
    } = EntityQuery.DefaultMaxTake;

    /// <summary>
    /// 是否包含已软删除的实体。
    /// </summary>
    public bool IncludeDeleted { get; init; }

    /// <summary>
    /// 派生带过滤的查询。
    /// </summary>
    public EntityQuery<T> Where(ISpecification<T> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        // 已有过滤时与新条件取 And；先把已有的 ISpecification 包成 Specification 才能使用组合方法
        return this with { Filter = Filter is null ? filter : new Specification<T>(Filter.Criteria).And(filter) };
    }

    /// <summary>
    /// 派生追加排序的查询。
    /// </summary>
    public EntityQuery<T> OrderBy(Expression<Func<T, object?>> keySelector, bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return this with { Orderings = [.. Orderings, new Ordering<T>(keySelector, descending)] };
    }

    /// <summary>
    /// 派生分页的查询。
    /// </summary>
    public EntityQuery<T> Page(int skip, int take) => this with { Skip = skip, Take = take };
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Queries;

/// <summary>
/// 实体查询
/// </summary>
/// <remarks>
/// 过滤、排序、分页。不可变，用 <c>with</c> 或链式方法派生
/// </remarks>
/// <typeparam name="T">实体类型</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record EntityQuery<T>
{
    /// <summary>
    /// 本版查询条件的修订号
    /// </summary>
    /// <remarks>
    /// Provider 在入口以 <c>Validate(EntityQuery&lt;T&gt;.Revision)</c> 传入，常量在编译时写进 Provider，遇到更新的条件即失败；以后新增条件时加 1，新条件记在新修订号下（RFC-0001「Provider 契约的演进」，ADR-0228）。修订 1：5.0 的全部条件
    /// </remarks>
    public const int Revision = 1;

    /// <summary>
    /// 这次查询用到的条件里最高的修订号
    /// </summary>
    /// <remarks>
    /// 没有用到新条件时不变，旧 Provider 照常服务。5.0 的条件都属于修订 1；以后新增条件时改为实例方法，按设置了的条件取其中最高的修订号，不要直接返回 <see cref="Revision"/>
    /// </remarks>
    private static int RequiredRevision() => 1;

    /// <summary>
    /// 核对这次用到的条件 Provider 是否都认识
    /// </summary>
    /// <param name="supportedRevision">Provider 编译时取到的 <see cref="Revision"/></param>
    /// <returns>成功；用到了更新的条件时为 <c>KERNEL_VALIDATION_007</c></returns>
    public Result Validate(int supportedRevision) => QueryRevision.Check(nameof(EntityQuery<T>), RequiredRevision(), supportedRevision);

    /// <summary>
    /// 过滤规约
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 为不过滤
    /// </remarks>
    public ISpecification<T>? Filter { get; init; }

    /// <summary>
    /// 排序，按顺序应用
    /// </summary>
    public IReadOnlyList<Ordering<T>> Orderings { get; init; } = [];

    /// <summary>
    /// 跳过条数
    /// </summary>
    public int Skip
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "'Skip' must not be negative.");
    }

    /// <summary>
    /// 取出条数上限
    /// </summary>
    /// <remarks>
    /// 1 到 <see cref="EntityQuery.DefaultMaxTake"/>，缺省取上限。大于上限是调用方可以改正的输入（上限是存储一侧的约束，调用方事先不知道）：抛出携带 <see cref="PersistenceErrors.TakeExceeded"/> 的 <see cref="FrameworkException"/>，调用里由 Transaction 阶段回滚并转为同码的校验结果（HTTP 400），之外仍是异常。小于 1 构不成查询，是编程错误，抛出 <see cref="ArgumentOutOfRangeException"/>。不静默截断：要更多行就分页。Provider 不得返回超过此数的结果
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

    /// <summary>
    /// 是否包含已软删除的实体
    /// </summary>
    public bool IncludeDeleted { get; init; }

    /// <summary>
    /// 时间范围提示
    /// </summary>
    /// <remarks>
    /// 按时间分表 / 分区的 Provider 据此只扫描范围内的表（SqlSugar 分表按 <c>[SplitField]</c> 列的 UTC 时刻选表）。它不是过滤条件：不分表的实体与不支持范围的 Provider 忽略它，范围之外的行照常返回；要按时间过滤另写规约。<see langword="null"/> 为不限定
    /// </remarks>
    public TimeRange? TimeRange { get; init; }

    /// <summary>
    /// 派生带过滤的查询
    /// </summary>
    public EntityQuery<T> Where(ISpecification<T> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        // 已有过滤时与新条件取 And；先把已有的 ISpecification 包成 Specification 才能使用组合方法
        return this with { Filter = Filter is null ? filter : new Specification<T>(Filter.Criteria).And(filter) };
    }

    /// <summary>
    /// 派生追加排序的查询
    /// </summary>
    /// <remarks>
    /// <c>descending</c> 取 <c>false</c>
    /// </remarks>
    public EntityQuery<T> OrderBy(Expression<Func<T, object?>> keySelector)
        => OrderBy(keySelector, false);

    /// <summary>
    /// 派生追加排序的查询
    /// </summary>
    public EntityQuery<T> OrderBy(Expression<Func<T, object?>> keySelector, bool descending)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return this with { Orderings = [.. Orderings, new Ordering<T>(keySelector, descending)] };
    }

    /// <summary>
    /// 派生分页的查询
    /// </summary>
    /// <remarks>
    /// <paramref name="skip"/> 为负、<paramref name="take"/> 小于 1 抛出 <see cref="ArgumentOutOfRangeException"/>；<paramref name="take"/> 大于 <see cref="EntityQuery.DefaultMaxTake"/> 抛出携带 <see cref="PersistenceErrors.TakeExceeded"/> 的 <see cref="FrameworkException"/>（调用里转为校验结果）
    /// </remarks>
    public EntityQuery<T> Page(int skip, int take) => this with { Skip = skip, Take = take };

    /// <summary>
    /// 派生带时间范围提示的查询（两端都含）
    /// </summary>
    /// <remarks>
    /// 只影响分表 / 分区实体扫描的表，不过滤行
    /// </remarks>
    public EntityQuery<T> Within(DateTimeOffset from, DateTimeOffset to) => this with { TimeRange = new TimeRange(from, to) };
}

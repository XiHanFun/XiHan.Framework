// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Queries;

/// <summary>
/// <see cref="EntityQuery{T}"/> 的工厂与常量。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class EntityQuery
{
    /// <summary>
    /// 页大小上限，也是缺省页大小。
    /// </summary>
    /// <remarks>
    /// <see cref="EntityQuery{T}.Take"/> 超过它在构造查询时抛出携带 <see cref="PersistenceErrors.TakeExceeded"/> 的 <see cref="FrameworkException"/>，调用里由 Transaction 阶段转为同码的校验结果；不静默截断。
    /// </remarks>
    public const int DefaultMaxTake = 1_000;

    /// <summary>
    /// 无条件、默认分页。
    /// </summary>
    public static EntityQuery<T> All<T>() => AllOf<T>.Instance;

    /// <summary>
    /// 按类型缓存无条件查询。
    /// </summary>
    /// <remarks>
    /// 查询不可变，共享同一实例是安全的。
    /// </remarks>
    private static class AllOf<T>
    {
        /// <summary>
        /// 该类型的无条件查询单例。
        /// </summary>
        public static readonly EntityQuery<T> Instance = new();
    }
}

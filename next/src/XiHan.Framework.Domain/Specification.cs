// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Domain;

/// <summary>
/// <see cref="Specification{T}"/> 的工厂。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class Specification
{
    /// <summary>
    /// 恒真规约。
    /// </summary>
    public static Specification<T> All<T>() => AllOf<T>.Instance;

    /// <summary>
    /// 用表达式创建规约。
    /// </summary>
    public static Specification<T> Of<T>(Expression<Func<T, bool>> criteria) => new(criteria);

    /// <summary>
    /// 按类型缓存恒真规约，避免每次调用 <see cref="All{T}"/> 都新建实例。
    /// </summary>
    private static class AllOf<T>
    {
        /// <summary>
        /// 该类型的恒真规约单例。
        /// </summary>
        public static readonly Specification<T> Instance = new(static _ => true);
    }
}

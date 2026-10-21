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

    private static class AllOf<T>
    {
        public static readonly Specification<T> Instance = new(static _ => true);
    }
}

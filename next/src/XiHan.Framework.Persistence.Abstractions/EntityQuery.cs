// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// <see cref="EntityQuery{T}"/> 的工厂与常量。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class EntityQuery
{
    /// <summary>
    /// 默认页大小上限。
    /// </summary>
    public const int DefaultMaxTake = 1_000;

    /// <summary>
    /// 无条件、默认分页。
    /// </summary>
    public static EntityQuery<T> All<T>() => AllOf<T>.Instance;

    private static class AllOf<T>
    {
        public static readonly EntityQuery<T> Instance = new();
    }
}

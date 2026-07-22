// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Uow.Extensions;

/// <summary>
/// 工作单元缓存项扩展
/// </summary>
public static class UnitOfWorkCacheItemExtensions
{
    /// <summary>
    /// 获取未移除的值
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="item"></param>
    /// <returns></returns>
    public static TValue? GetUnRemovedValueOrNull<TValue>(this UnitOfWorkCacheItem<TValue>? item)
        where TValue : class
    {
        return item is not null && !item.IsRemoved ? item.Value : null;
    }
}

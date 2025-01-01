#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkCacheItemExtensions
// Guid:d71d323d-3331-4f32-bc34-df6f72329f3e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 5:40:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Extensions;

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
        return item != null && !item.IsRemoved ? item.Value : null;
    }
}

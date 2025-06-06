﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheNameAttribute
// Guid:9e786d9b-c617-4dc7-83f2-4fad3e35a548
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 5:48:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.System;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Caching;

/// <summary>
/// 缓存名称特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public class CacheNameAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    public CacheNameAttribute(string name)
    {
        _ = Guard.NotNull(name, nameof(name));

        Name = name;
    }

    /// <summary>
    /// 缓存名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取缓存名称
    /// </summary>
    /// <typeparam name="TCacheItem"></typeparam>
    /// <returns></returns>
    public static string GetCacheName<TCacheItem>()
    {
        return GetCacheName(typeof(TCacheItem));
    }

    /// <summary>
    /// 获取缓存名称
    /// </summary>
    /// <param name="cacheItemType"></param>
    /// <returns></returns>
    public static string GetCacheName(Type cacheItemType)
    {
        var cacheNameAttribute = cacheItemType
            .GetCustomAttributes(true)
            .OfType<CacheNameAttribute>()
            .FirstOrDefault();

        return cacheNameAttribute is not null ? cacheNameAttribute.Name : cacheItemType.FullName!.RemovePostFix("CacheItem");
    }
}

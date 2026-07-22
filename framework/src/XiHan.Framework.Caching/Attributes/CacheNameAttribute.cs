// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Caching.Attributes;

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
        Guard.NotNull(name, nameof(name));

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

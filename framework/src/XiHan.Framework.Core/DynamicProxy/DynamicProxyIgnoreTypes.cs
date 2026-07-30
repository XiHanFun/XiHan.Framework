// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Core.DynamicProxy;

/// <summary>
/// 动态代理忽略类型
/// </summary>
public class DynamicProxyIgnoreTypes
{
    /// <summary>
    /// 忽略类型
    /// </summary>
    private static HashSet<Type> IgnoredTypes { get; } = [];

    /// <summary>
    /// 添加
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static void Add<T>()
    {
        Add(typeof(T));
    }

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="type"></param>
    public static void Add(Type type)
    {
        lock (IgnoredTypes)
        {
            IgnoredTypes.AddIfNotContains(type);
        }
    }

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="types"></param>
    public static void Add(params Type[] types)
    {
        lock (IgnoredTypes)
        {
            IgnoredTypes.AddIfNotContains(types);
        }
    }

    /// <summary>
    /// 包含
    /// </summary>
    /// <param name="type"></param>
    /// <param name="includeDerivedTypes"></param>
    /// <returns></returns>
    public static bool Contains(Type type, bool includeDerivedTypes = true)
    {
        lock (IgnoredTypes)
        {
            return includeDerivedTypes
                ? IgnoredTypes.Any(t => t.IsAssignableFrom(type))
                : IgnoredTypes.Contains(type);
        }
    }
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheInterceptorRegistrar
// Guid:c9d0e1f2-a3b4-5678-2345-6789abcdef01
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Caching.Interceptors;

/// <summary>
/// 缓存拦截器注册器，在服务注册时自动为包含 [Cacheable] 方法的类型添加缓存拦截器
/// </summary>
public static class CacheInterceptorRegistrar
{
    /// <summary>
    /// 按需注册缓存拦截器
    /// </summary>
    /// <param name="context"></param>
    public static void RegisterIfNeeded(IOnServiceRegistredContext context)
    {
        if (ShouldIntercept(context.ImplementationType))
        {
            context.Interceptors.TryAdd<CacheInterceptor>();
        }
    }

    private static bool ShouldIntercept(Type type)
    {
        if (DynamicProxyIgnoreTypes.Contains(type))
        {
            return false;
        }

        return HasCacheableMethod(type);
    }

    private static bool HasCacheableMethod(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        return methods.Any(m =>
            m.IsDefined(typeof(CacheableAttribute), true) ||
            m.IsDefined(typeof(CacheEvictAttribute), true));
    }
}

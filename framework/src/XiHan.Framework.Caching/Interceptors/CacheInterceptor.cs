#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheInterceptor
// Guid:b8c9d0e1-f2a3-4567-1234-56789abcdef0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Hybrid;
using System.Collections.Concurrent;
using System.Reflection;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Caching.Interceptors;

/// <summary>
/// 缓存拦截器，自动为标记 [Cacheable] 的方法提供 AOP 缓存能力
/// </summary>
public class CacheInterceptor : XiHanInterceptor, ITransientDependency
{
    private readonly HybridCache _hybridCache;

    private static readonly ConcurrentDictionary<MethodInfo, CacheableAttribute?> CacheableAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, CacheEvictAttribute[]> CacheEvictAttributeCache = new();

    private static readonly MethodInfo InternalGetOrCreateMethodInfo =
        typeof(CacheInterceptor).GetMethod(nameof(InternalGetOrCreateAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="hybridCache"></param>
    public CacheInterceptor(HybridCache hybridCache)
    {
        _hybridCache = hybridCache;
    }

    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation"></param>
    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        var cacheableAttr = GetCacheableAttribute(invocation.Method);

        if (cacheableAttr != null)
        {
            await HandleCacheableAsync(invocation, cacheableAttr);
            return;
        }

        var evictAttrs = GetCacheEvictAttributes(invocation.Method);

        if (evictAttrs.Length > 0)
        {
            await invocation.ProceedAsync();
            await HandleCacheEvictAsync(invocation, evictAttrs);
            return;
        }

        await invocation.ProceedAsync();
    }

    private async Task HandleCacheableAsync(IXiHanMethodInvocation invocation, CacheableAttribute attr)
    {
        var cacheKey = CacheKeyBuilder.Build(attr.Key, invocation);
        var returnType = GetActualReturnType(invocation.Method);

        if (returnType == null)
        {
            await invocation.ProceedAsync();
            return;
        }

        var method = InternalGetOrCreateMethodInfo.MakeGenericMethod(returnType);
        var task = (Task)method.Invoke(this, [invocation, cacheKey, attr.ExpireSeconds])!;
        await task;
    }

    private async Task InternalGetOrCreateAsync<T>(
        IXiHanMethodInvocation invocation, string cacheKey, int expireSeconds)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(expireSeconds),
            LocalCacheExpiration = TimeSpan.FromSeconds(Math.Min(expireSeconds, 60))
        };

        var result = await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async _ =>
            {
                await invocation.ProceedAsync();
                var returnValue = invocation.ReturnValue;

                if (returnValue is Task<T> task)
                {
                    return await task;
                }

                return returnValue is T typedValue ? typedValue : default!;
            },
            options);

        invocation.ReturnValue = Task.FromResult(result);
    }

    private async Task HandleCacheEvictAsync(IXiHanMethodInvocation invocation, CacheEvictAttribute[] attrs)
    {
        foreach (var attr in attrs)
        {
            var cacheKey = CacheKeyBuilder.Build(attr.Key, invocation);
            // 方法执行成功后，按模板构建出的键直接从 HybridCache（L1 内存 + L2 分布式）移除
            await _hybridCache.RemoveAsync(cacheKey);
        }
    }

    private static CacheableAttribute? GetCacheableAttribute(MethodInfo method)
    {
        return CacheableAttributeCache.GetOrAdd(method, m =>
            m.GetCustomAttribute<CacheableAttribute>());
    }

    private static CacheEvictAttribute[] GetCacheEvictAttributes(MethodInfo method)
    {
        return CacheEvictAttributeCache.GetOrAdd(method, m =>
            m.GetCustomAttributes<CacheEvictAttribute>().ToArray());
    }

    private static Type? GetActualReturnType(MethodInfo method)
    {
        var returnType = method.ReturnType;

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        if (returnType != typeof(Task) && returnType != typeof(void))
        {
            return returnType;
        }

        return null;
    }
}

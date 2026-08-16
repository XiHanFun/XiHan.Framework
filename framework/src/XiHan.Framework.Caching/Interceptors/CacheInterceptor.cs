// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Caching.Interceptors;

/// <summary>
/// 缓存拦截器，自动为标记 [Cacheable] 的方法提供 AOP 缓存能力
/// </summary>
/// <remarks>
/// 只作用于经接口动态代理解析出来的服务；HTTP 入口的控制器不经过代理，由 Web 层的 MVC 缓存过滤器覆盖。
/// </remarks>
public class CacheInterceptor : XiHanInterceptor, ITransientDependency
{
    private readonly CacheAspect _cacheAspect;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="cacheAspect">缓存切面</param>
    public CacheInterceptor(CacheAspect cacheAspect)
    {
        _cacheAspect = cacheAspect;
    }

    /// <summary>
    /// 异步拦截
    /// </summary>
    /// <param name="invocation"></param>
    public override async Task InterceptAsync(IXiHanMethodInvocation invocation)
    {
        var cacheableAttr = CacheAspect.GetCacheableAttributeOrNull(invocation.Method);

        if (cacheableAttr != null)
        {
            await HandleCacheableAsync(invocation, cacheableAttr);
            return;
        }

        var evictAttrs = CacheAspect.GetCacheEvictAttributes(invocation.Method);

        if (evictAttrs.Length > 0)
        {
            await invocation.ProceedAsync();
            await _cacheAspect.EvictAsync(invocation.Method, invocation.Arguments, evictAttrs);
            return;
        }

        await invocation.ProceedAsync();
    }

    private async Task HandleCacheableAsync(IXiHanMethodInvocation invocation, CacheableAttribute attr)
    {
        var valueType = CacheAspect.GetCacheableValueTypeOrNull(invocation.Method);

        if (valueType == null)
        {
            await invocation.ProceedAsync();
            return;
        }

        var cacheKey = CacheKeyBuilder.Build(attr.Key, invocation);

        invocation.ReturnValue = (await _cacheAspect.GetOrCreateAsync(
            valueType,
            cacheKey,
            attr.ExpireSeconds,
            async () =>
            {
                await invocation.ProceedAsync();

                // 代理链上的异步方法把结果留在 ReturnValue 里的 Task 上，取值前先解包
                return invocation.ReturnValue is Task task ? await UnwrapAsync(task) : invocation.ReturnValue;
            }))!;
    }

    private static async Task<object?> UnwrapAsync(Task task)
    {
        await task;

        return task.GetType().GetProperty(nameof(Task<object>.Result))?.GetValue(task);
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using System.Collections.Concurrent;
using System.Reflection;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Caching.Interceptors;

/// <summary>
/// 缓存切面，承载 <see cref="CacheableAttribute"/> / <see cref="CacheEvictAttribute"/> 的读写语义
/// </summary>
/// <remarks>
/// 由 <see cref="CacheInterceptor"/>（进程内动态代理）与 Web 层的 MVC 缓存过滤器（HTTP 入口）共用，
/// 两条入口的键构建、过期时间与命中语义由此保持一致。
/// </remarks>
public class CacheAspect : ITransientDependency
{
    private static readonly ConcurrentDictionary<MethodInfo, CacheableAttribute?> CacheableAttributeCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, CacheEvictAttribute[]> CacheEvictAttributeCache = new();

    private static readonly MethodInfo GetOrCreateCoreMethodInfo =
        typeof(CacheAspect).GetMethod(nameof(GetOrCreateCoreAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly HybridCache _hybridCache;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="hybridCache">混合缓存</param>
    public CacheAspect(HybridCache hybridCache)
    {
        _hybridCache = hybridCache;
    }

    /// <summary>
    /// 获取方法上的可缓存特性
    /// </summary>
    /// <param name="method">方法</param>
    /// <returns>可缓存特性，未标注时为 null</returns>
    public static CacheableAttribute? GetCacheableAttributeOrNull(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return CacheableAttributeCache.GetOrAdd(method, m => m.GetCustomAttribute<CacheableAttribute>());
    }

    /// <summary>
    /// 获取方法上的缓存清除特性
    /// </summary>
    /// <param name="method">方法</param>
    /// <returns>缓存清除特性数组</returns>
    public static CacheEvictAttribute[] GetCacheEvictAttributes(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return CacheEvictAttributeCache.GetOrAdd(method, m => [.. m.GetCustomAttributes<CacheEvictAttribute>()]);
    }

    /// <summary>
    /// 获取方法可缓存的值类型，无返回值（void / Task）时为 null
    /// </summary>
    /// <param name="method">方法</param>
    /// <returns>值类型</returns>
    public static Type? GetCacheableValueTypeOrNull(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var returnType = method.ReturnType;

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        return returnType != typeof(Task) && returnType != typeof(void) ? returnType : null;
    }

    /// <summary>
    /// 读缓存，未命中时执行 <paramref name="valueFactory"/> 取值并写入
    /// </summary>
    /// <param name="valueType">缓存值类型</param>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="expireSeconds">过期秒数</param>
    /// <param name="valueFactory">未命中时的取值委托</param>
    /// <returns>缓存值</returns>
    public Task<object?> GetOrCreateAsync(Type valueType, string cacheKey, int expireSeconds, Func<Task<object?>> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(valueFactory);

        var method = GetOrCreateCoreMethodInfo.MakeGenericMethod(valueType);

        return (Task<object?>)method.Invoke(this, [cacheKey, expireSeconds, valueFactory])!;
    }

    /// <summary>
    /// 按特性上的键模板清除缓存
    /// </summary>
    /// <param name="method">方法，占位符按其形参名匹配</param>
    /// <param name="arguments">与形参一一对应的实参</param>
    /// <param name="attributes">缓存清除特性</param>
    /// <returns>异步任务</returns>
    public async Task EvictAsync(MethodInfo method, IReadOnlyList<object?> arguments, IReadOnlyList<CacheEvictAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        foreach (var attribute in attributes)
        {
            // 方法执行成功后，按模板构建出的键直接从 HybridCache（L1 内存 + L2 分布式）移除
            await _hybridCache.RemoveAsync(CacheKeyBuilder.Build(attribute.Key, method, arguments));
        }
    }

    private async Task<object?> GetOrCreateCoreAsync<T>(string cacheKey, int expireSeconds, Func<Task<object?>> valueFactory)
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
                var produced = await valueFactory();

                return produced is T typedValue ? typedValue : default!;
            },
            options);

        return result;
    }
}

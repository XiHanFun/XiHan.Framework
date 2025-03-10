#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CachedServiceProviderBase
// Guid:13f2bcb3-29b1-46a8-93f5-3eae9e5f06e2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 2:52:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 已缓存服务提供器基类
/// </summary>
public abstract class CachedServiceProviderBase : ICachedServiceProviderBase
{
    /// <summary>
    /// 服务提供器
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 缓存的服务集合
    /// </summary>
    protected ConcurrentDictionary<ServiceIdentifier, Lazy<object?>> CachedServices { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    protected CachedServiceProviderBase(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        CachedServices = new ConcurrentDictionary<ServiceIdentifier, Lazy<object?>>();
        _ = CachedServices.TryAdd(new ServiceIdentifier(typeof(IServiceProvider)), new Lazy<object?>(() => ServiceProvider));
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <returns></returns>
    public virtual object? GetService(Type serviceType)
    {
        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceType),
            _ => new Lazy<object?>(() => ServiceProvider.GetService(serviceType))
        ).Value;
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public T GetService<T>(T defaultValue)
    {
        return (T)GetService(typeof(T), defaultValue!);
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public object GetService(Type serviceType, object defaultValue)
    {
        return GetService(serviceType) ?? defaultValue;
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="factory"></param>
    /// <returns></returns>
    public T GetService<T>(Func<IServiceProvider, object> factory)
    {
        return (T)GetService(typeof(T), factory);
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    public object GetService(Type serviceType, Func<IServiceProvider, object> factory)
    {
        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceType),
            _ => new Lazy<object?>(() => factory(ServiceProvider))
        ).Value!;
    }

    /// <summary>
    /// 获取键控服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="serviceKey"></param>
    /// <returns></returns>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceKey, serviceType),
            _ => new Lazy<object?>(() => ServiceProvider.GetKeyedService(serviceType, serviceKey))
        ).Value;
    }

    /// <summary>
    /// 获取请求服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="serviceKey"></param>
    /// <returns></returns>
    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        return CachedServices.GetOrAdd(
            new ServiceIdentifier(serviceKey, serviceType),
            _ => new Lazy<object?>(() => ServiceProvider.GetRequiredKeyedService(serviceType, serviceKey))
        ).Value!;
    }
}

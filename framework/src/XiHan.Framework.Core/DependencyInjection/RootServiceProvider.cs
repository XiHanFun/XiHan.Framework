// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 根服务提供者
/// </summary>
[ExposeServices(typeof(IRootServiceProvider))]
public class RootServiceProvider : IRootServiceProvider, ISingletonDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="objectAccessor"></param>
    public RootServiceProvider(IObjectAccessor<IServiceProvider> objectAccessor)
    {
        ServiceProvider = objectAccessor.Value!;
    }

    /// <summary>
    /// 服务提供者
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <returns></returns>
    public virtual object? GetService(Type serviceType)
    {
        return ServiceProvider.GetService(serviceType);
    }

    /// <summary>
    /// 获取键控服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="serviceKey"></param>
    /// <returns></returns>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        return ServiceProvider.GetKeyedService(serviceType, serviceKey);
    }

    /// <summary>
    /// 获取请求键控服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="serviceKey"></param>
    /// <returns></returns>
    public virtual object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        return ServiceProvider.GetRequiredKeyedService(serviceType, serviceKey);
    }
}

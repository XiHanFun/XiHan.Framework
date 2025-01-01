#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RootServiceProvider
// Guid:6397c98f-4100-4a27-8a12-a68852ab6d31
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/27 22:31:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 根服务提供者
/// </summary>
[ExposeServices(typeof(IRootServiceProvider))]
public class RootServiceProvider : IRootServiceProvider, ISingletonDependency
{
    /// <summary>
    /// 服务提供者
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="objectAccessor"></param>
    public RootServiceProvider(IObjectAccessor<IServiceProvider> objectAccessor)
    {
        ServiceProvider = objectAccessor.Value!;
    }

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

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 已缓存服务提供器
/// </summary>
[ExposeServices(typeof(ICachedServiceProvider))]
public class CachedServiceProvider : CachedServiceProviderBase, ICachedServiceProvider, IScopedDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    public CachedServiceProvider(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }
}

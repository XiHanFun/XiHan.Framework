// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Interceptors;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Serialization;
using XiHan.Framework.Threading;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Caching;

/// <summary>
/// 曦寒框架缓存模块
/// </summary>
[DependsOn(
    typeof(XiHanMultiTenancyAbstractionsModule),
    typeof(XiHanSerializationModule),
    typeof(XiHanThreadingModule),
    typeof(XiHanUowModule)
    )]
public class XiHanCachingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanCaching(config);

        services.OnRegistered(CacheInterceptorRegistrar.RegisterIfNeeded);
    }
}

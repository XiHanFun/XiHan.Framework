#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionCastleExtensions
// Guid:8c89c7e2-ff7f-4865-90e0-6b3d92b24872
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Castle.Extensions;

/// <summary>
/// Castle 动态代理服务集合扩展
/// </summary>
public static class ServiceCollectionCastleExtensions
{
    private static readonly ProxyGenerator ProxyGeneratorInstance = new();

    /// <summary>
    /// 启用 Castle 动态代理，为注册了拦截器的服务自动创建代理
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddCastleDynamicProxy(this IServiceCollection services)
    {
        if (services.IsClassInterceptorsDisabled())
        {
            return services;
        }

        var actionList = services.GetRegistrationActionList();
        if (actionList.Count == 0)
        {
            return services;
        }

        var descriptorsToProxy = new List<(int index, ServiceDescriptor descriptor, IOnServiceRegistredContext context)>();

        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];

            if (descriptor.ServiceType.IsInterface &&
                descriptor.ImplementationType != null &&
                !DynamicProxyIgnoreTypes.Contains(descriptor.ImplementationType))
            {
                var context = new OnServiceRegistredContext(descriptor.ServiceType, descriptor.ImplementationType);

                foreach (var action in actionList)
                {
                    action(context);
                }

                if (context.Interceptors.Count > 0)
                {
                    descriptorsToProxy.Add((i, descriptor, context));
                }
            }
        }

        foreach (var (index, descriptor, context) in descriptorsToProxy)
        {
            var proxyDescriptor = CreateProxiedDescriptor(descriptor, context);
            services[index] = proxyDescriptor;
        }

        return services;
    }

    private static ServiceDescriptor CreateProxiedDescriptor(
        ServiceDescriptor original,
        IOnServiceRegistredContext context)
    {
        var interceptorTypes = context.Interceptors.ToArray();

        return ServiceDescriptor.Describe(
            original.ServiceType,
            sp =>
            {
                var target = CreateOriginalInstance(sp, original);
                var interceptors = ResolveInterceptors(sp, interceptorTypes);
                var adapter = new CastleInterceptorAdapter(interceptors);

                return ProxyGeneratorInstance.CreateInterfaceProxyWithTarget(
                    original.ServiceType,
                    target,
                    adapter);
            },
            original.Lifetime);
    }

    private static object CreateOriginalInstance(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance != null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return descriptor.ImplementationFactory(sp);
        }

        return ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
    }

    private static IXiHanInterceptor[] ResolveInterceptors(IServiceProvider sp, Type[] interceptorTypes)
    {
        var interceptors = new IXiHanInterceptor[interceptorTypes.Length];

        for (var i = 0; i < interceptorTypes.Length; i++)
        {
            interceptors[i] = (IXiHanInterceptor)sp.GetRequiredService(interceptorTypes[i]);
        }

        return interceptors;
    }
}

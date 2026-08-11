// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        var implementationTypeRegistry = services.GetImplementationTypeRegistry();
        var descriptorsToProxy = new List<(int Index, ServiceDescriptor Descriptor, Type ImplementationType, IOnServiceRegistredContext Context)>();

        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];

            if (!descriptor.ServiceType.IsInterface)
            {
                continue;
            }

            var implementationType = implementationTypeRegistry.ResolveImplementationTypeOrNull(descriptor);
            if (implementationType is null || DynamicProxyIgnoreTypes.Contains(implementationType))
            {
                continue;
            }

            var context = new OnServiceRegistredContext(descriptor.ServiceType, implementationType);

            foreach (var action in actionList)
            {
                action(context);
            }

            if (context.Interceptors.Count > 0)
            {
                descriptorsToProxy.Add((i, descriptor, implementationType, context));
            }
        }

        foreach (var (index, descriptor, implementationType, context) in descriptorsToProxy)
        {
            services[index] = CreateProxiedDescriptor(descriptor, implementationType, context);
        }

        return services;
    }

    /// <summary>
    /// 创建包装为代理的服务描述器，保持原描述器的生命周期与服务键
    /// </summary>
    /// <param name="original"></param>
    /// <param name="implementationType"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    private static ServiceDescriptor CreateProxiedDescriptor(
        ServiceDescriptor original,
        Type implementationType,
        IOnServiceRegistredContext context)
    {
        var interceptorTypes = context.Interceptors.ToArray();
        var serviceType = original.ServiceType;

        return original.IsKeyedService
            ? ServiceDescriptor.DescribeKeyed(
                serviceType,
                original.ServiceKey,
                (sp, key) => CreateProxy(sp, serviceType, CreateOriginalKeyedInstance(sp, key, original, implementationType), interceptorTypes),
                original.Lifetime)
            : ServiceDescriptor.Describe(
                serviceType,
                sp => CreateProxy(sp, serviceType, CreateOriginalInstance(sp, original, implementationType), interceptorTypes),
                original.Lifetime);
    }

    /// <summary>
    /// 以目标实例创建接口代理
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="serviceType"></param>
    /// <param name="target"></param>
    /// <param name="interceptorTypes"></param>
    /// <returns></returns>
    private static object CreateProxy(IServiceProvider sp, Type serviceType, object target, Type[] interceptorTypes)
    {
        var adapter = new CastleInterceptorAdapter(ResolveInterceptors(sp, interceptorTypes));

        return ProxyGeneratorInstance.CreateInterfaceProxyWithTarget(serviceType, target, adapter);
    }

    /// <summary>
    /// 按原描述器的注册方式创建目标实例
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="descriptor"></param>
    /// <param name="implementationType"></param>
    /// <returns></returns>
    private static object CreateOriginalInstance(IServiceProvider sp, ServiceDescriptor descriptor, Type implementationType)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        return descriptor.ImplementationFactory is not null
            ? descriptor.ImplementationFactory(sp)
            : ActivatorUtilities.CreateInstance(sp, implementationType);
    }

    /// <summary>
    /// 按原键值描述器的注册方式创建目标实例
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="serviceKey"></param>
    /// <param name="descriptor"></param>
    /// <param name="implementationType"></param>
    /// <returns></returns>
    private static object CreateOriginalKeyedInstance(IServiceProvider sp, object? serviceKey, ServiceDescriptor descriptor, Type implementationType)
    {
        if (descriptor.KeyedImplementationInstance is not null)
        {
            return descriptor.KeyedImplementationInstance;
        }

        return descriptor.KeyedImplementationFactory is not null
            ? descriptor.KeyedImplementationFactory(sp, serviceKey)
            : ActivatorUtilities.CreateInstance(sp, implementationType);
    }

    /// <summary>
    /// 解析拦截器实例
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="interceptorTypes"></param>
    /// <returns></returns>
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

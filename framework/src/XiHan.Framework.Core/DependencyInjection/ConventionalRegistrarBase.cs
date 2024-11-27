#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConventionalRegistrarBase
// Guid:0f5c3a76-b546-4fd9-bacd-dbf16aea62fa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:28:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Utils.Reflections;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 常规注册器基类
/// </summary>
public abstract class ConventionalRegistrarBase : IConventionalRegistrar
{
    /// <summary>
    /// 添加程序集
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    public virtual void AddAssembly(IServiceCollection services, Assembly assembly)
    {
        var types = AssemblyHelper.GetAllTypes(assembly)
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericType: false })
            .ToArray();

        AddTypes(services, types);
    }

    /// <summary>
    /// 添加多个类型
    /// </summary>
    /// <param name="services"></param>
    /// <param name="types"></param>
    public virtual void AddTypes(IServiceCollection services, params Type[] types)
    {
        foreach (var type in types)
        {
            AddType(services, type);
        }
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    public abstract void AddType(IServiceCollection services, Type type);

    /// <summary>
    /// 常规注册是否被禁用
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected virtual bool IsConventionalRegistrationDisabled(Type type)
    {
        return type.IsDefined(typeof(DisableConventionalRegistrationAttribute), true);
    }

    /// <summary>
    /// 触发服务暴露
    /// </summary>
    /// <param name="services"></param>
    /// <param name="implementationType"></param>
    /// <param name="serviceTypes"></param>
    protected virtual void TriggerServiceExposing(IServiceCollection services, Type implementationType, List<Type> serviceTypes)
    {
        TriggerServiceExposing(services, implementationType, serviceTypes.ConvertAll(t => new ServiceIdentifier(t)));
    }

    /// <summary>
    /// 触发服务暴露
    /// </summary>
    /// <param name="services"></param>
    /// <param name="implementationType"></param>
    /// <param name="serviceTypes"></param>
    protected virtual void TriggerServiceExposing(IServiceCollection services, Type implementationType, List<ServiceIdentifier> serviceTypes)
    {
        var exposeActions = services.GetExposingActionList();
        if (!exposeActions.Any())
        {
            return;
        }

        OnServiceExposingContext args = new(implementationType, serviceTypes);
        foreach (var action in exposeActions)
        {
            action(args);
        }
    }

    /// <summary>
    /// 获取依赖特性
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected virtual DependencyAttribute? GetDependencyAttributeOrNull(Type type)
    {
        return type.GetCustomAttribute<DependencyAttribute>(true);
    }

    /// <summary>
    /// 获取生命周期
    /// </summary>
    /// <param name="type"></param>
    /// <param name="dependencyAttribute"></param>
    /// <returns></returns>
    protected virtual ServiceLifetime? GetLifeTimeOrNull(Type type, DependencyAttribute? dependencyAttribute)
    {
        return dependencyAttribute?.Lifetime ?? GetServiceLifetimeFromClassHierarchy(type) ?? GetDefaultLifeTimeOrNull();
    }

    /// <summary>
    /// 从类层次结构中获取服务生命周期
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected virtual ServiceLifetime? GetServiceLifetimeFromClassHierarchy(Type type)
    {
        return typeof(ITransientDependency).GetTypeInfo().IsAssignableFrom(type)
            ? ServiceLifetime.Transient
            : typeof(ISingletonDependency).GetTypeInfo().IsAssignableFrom(type)
            ? ServiceLifetime.Singleton
            : typeof(IScopedDependency).GetTypeInfo().IsAssignableFrom(type) ? ServiceLifetime.Scoped : null;
    }

    /// <summary>
    /// 获取默认生命周期
    /// </summary>
    /// <returns></returns>
    protected virtual ServiceLifetime? GetDefaultLifeTimeOrNull()
    {
        return null;
    }

    /// <summary>
    /// 获取暴露的服务类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected virtual List<Type> GetExposedServiceTypes(Type type)
    {
        return ExposedServiceExplorer.GetExposedServices(type);
    }

    /// <summary>
    /// 获取暴露的键值服务类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected virtual List<ServiceIdentifier> GetExposedKeyedServiceTypes(Type type)
    {
        return ExposedServiceExplorer.GetExposedKeyedServices(type);
    }

    /// <summary>
    /// 创建服务描述器
    /// </summary>
    /// <param name="implementationType"></param>
    /// <param name="serviceKey"></param>
    /// <param name="exposingServiceType"></param>
    /// <param name="allExposingServiceTypes"></param>
    /// <param name="lifeTime"></param>
    /// <returns></returns>
    protected virtual ServiceDescriptor CreateServiceDescriptor(Type implementationType, object? serviceKey,
        Type exposingServiceType, List<ServiceIdentifier> allExposingServiceTypes, ServiceLifetime lifeTime)
    {
        if (!lifeTime.IsIn(ServiceLifetime.Singleton, ServiceLifetime.Scoped))
        {
            return serviceKey == null
                ? ServiceDescriptor.Describe(exposingServiceType, implementationType, lifeTime)
                : ServiceDescriptor.DescribeKeyed(exposingServiceType, serviceKey, implementationType, lifeTime);
        }

        var redirectedType = GetRedirectedTypeOrNull(implementationType, exposingServiceType, allExposingServiceTypes);

        if (redirectedType != null)
        {
            return serviceKey == null
                ? ServiceDescriptor.Describe(exposingServiceType, provider => provider.GetService(redirectedType)!, lifeTime)
                : ServiceDescriptor.DescribeKeyed(exposingServiceType, serviceKey, (provider, key) => provider.GetKeyedService(redirectedType, key)!, lifeTime);
        }

        return serviceKey == null
            ? ServiceDescriptor.Describe(exposingServiceType, implementationType, lifeTime)
            : ServiceDescriptor.DescribeKeyed(exposingServiceType, serviceKey, implementationType, lifeTime);
    }

    /// <summary>
    /// 获取重定向类型或空
    /// </summary>
    /// <param name="implementationType"></param>
    /// <param name="exposingServiceType"></param>
    /// <param name="allExposingKeyedServiceTypes"></param>
    /// <returns></returns>
    protected virtual Type? GetRedirectedTypeOrNull(Type implementationType, Type exposingServiceType, List<ServiceIdentifier> allExposingKeyedServiceTypes)
    {
        return allExposingKeyedServiceTypes.Count < 2
            ? null
            : exposingServiceType == implementationType
            ? null
            : allExposingKeyedServiceTypes.Any(t => t.ServiceType == implementationType)
            ? implementationType
            : allExposingKeyedServiceTypes.FirstOrDefault(
            t => t.ServiceType != exposingServiceType && exposingServiceType.IsAssignableFrom(t.ServiceType)
        ).ServiceType;
    }
}
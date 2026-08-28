// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 默认常规注册器
/// </summary>
public class DefaultConventionalRegistrar : ConventionalRegistrarBase
{
    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    public override void AddType(IServiceCollection services, Type type)
    {
        if (IsConventionalRegistrationDisabled(type))
        {
            return;
        }

        var dependencyAttribute = GetDependencyAttributeOrNull(type);
        var lifeTime = GetLifeTimeOrNull(type, dependencyAttribute);

        if (lifeTime is null)
        {
            return;
        }

        var exposedServiceAndKeyedServiceTypes = GetExposedKeyedServiceTypes(type)
            .Concat(GetExposedServiceTypes(type)
            .Select(t => new ServiceIdentifier(t))).ToList();

        TriggerServiceExposing(services, type, exposedServiceAndKeyedServiceTypes);

        // 键值暴露与非键值暴露各自独立计算 allExposingServiceTypes，这是刻意的分组而非疏漏：
        // 重定向后的键值描述器走 provider.GetKeyedService(redirectedType, key) 解析，只能命中同一个 key 下的注册，
        // 跨组重定向在容器里根本解析不到。因此同一实现类型同时声明 [ExposeServices] 与 [ExposeKeyedService<T>] 时，
        // 键值门面与非键值门面是两条互不相干的注册，即便生命周期是 Singleton 也各自持有一个实例。
        // 若要让同一组内的多个门面共享实例，把实现类型自身也列进该组的暴露类型，重定向便会指向它；
        // 跨组共享不在当前契约范围内。
        foreach (var serviceDescriptor in from exposedServiceType in exposedServiceAndKeyedServiceTypes
                                          let allExposingServiceTypes = exposedServiceType.ServiceKey is null
                                              ? exposedServiceAndKeyedServiceTypes.Where(x => x.ServiceKey is null).ToList()
                                              : [.. exposedServiceAndKeyedServiceTypes.Where(x => x.ServiceKey?.ToString() == exposedServiceType.ServiceKey?.ToString())]
                                          select CreateServiceDescriptor(type, exposedServiceType.ServiceKey, exposedServiceType.ServiceType, allExposingServiceTypes, lifeTime.Value))
        {
            TrackImplementationType(services, serviceDescriptor, type);

            if (dependencyAttribute?.ReplaceServices == true)
            {
                services.Replace(serviceDescriptor);
            }
            else if (dependencyAttribute?.TryRegister == true)
            {
                services.TryAdd(serviceDescriptor);
            }
            else
            {
                services.Add(serviceDescriptor);
            }
        }
    }
}

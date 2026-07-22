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

        foreach (var serviceDescriptor in from exposedServiceType in exposedServiceAndKeyedServiceTypes
                                          let allExposingServiceTypes = exposedServiceType.ServiceKey is null
                                              ? exposedServiceAndKeyedServiceTypes.Where(x => x.ServiceKey is null).ToList()
                                              : [.. exposedServiceAndKeyedServiceTypes.Where(x => x.ServiceKey?.ToString() == exposedServiceType.ServiceKey?.ToString())]
                                          select CreateServiceDescriptor(type, exposedServiceType.ServiceKey, exposedServiceType.ServiceType, allExposingServiceTypes, lifeTime.Value))
        {
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

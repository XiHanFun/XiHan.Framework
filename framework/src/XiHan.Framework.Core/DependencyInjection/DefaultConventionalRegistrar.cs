#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultConventionalRegistrar
// Guid:7e90c0d7-85d3-4b32-a86a-0ef44809b21d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 01:28:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 暴露服务探测器
/// </summary>
public class ExposedServiceExplorer
{
    /// <summary>
    /// 默认暴露服务特性
    /// </summary>
    private static readonly ExposeServicesAttribute DefaultExposeServicesAttribute =
        new()
        {
            IncludeDefaults = true,
            IncludeSelf = true
        };

    /// <summary>
    /// 获取暴露的服务
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static List<Type> GetExposedServices(Type type)
    {
        var exposedServiceTypesProviders = type.GetCustomAttributes(true).OfType<IExposedServiceTypesProvider>().ToList();

        if (exposedServiceTypesProviders.IsNullOrEmpty() && type.GetCustomAttributes(true).OfType<IExposedKeyedServiceTypesProvider>().Any())
        {
            // 如果有任何 IExposedKeyedServiceTypesProvider，但没有 IExposedServiceTypesProvider，将不会公开默认服务
            return [];
        }

        return [.. exposedServiceTypesProviders.DefaultIfEmpty(DefaultExposeServicesAttribute)
            .SelectMany(p => p.GetExposedServiceTypes(type))
            .Distinct()];
    }

    /// <summary>
    /// 获取暴露的键值服务
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static List<ServiceIdentifier> GetExposedKeyedServices(Type type)
    {
        return [.. type.GetCustomAttributes(true).OfType<IExposedKeyedServiceTypesProvider>()
            .SelectMany(p => p.GetExposedServiceTypes(type))
            .Distinct()];
    }
}

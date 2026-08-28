// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务集合配置扩展
/// </summary>
public static class ServiceCollectionConfigurationExtensions
{
    /// <summary>
    /// 替换配置
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection ReplaceConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        return services.Replace(ServiceDescriptor.Singleton(configuration));
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public static IConfiguration GetConfiguration(this IServiceCollection services)
    {
        return services.GetConfigurationOrNull() ??
            throw new XiHanException($"在服务集合中找不到{typeof(IConfiguration).AssemblyQualifiedName}的实现。");
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IConfiguration? GetConfigurationOrNull(this IServiceCollection services)
    {
        var hostBuilderContext = services.GetSingletonInstanceOrNull<HostBuilderContext>();

        // 原写法是 `Configuration is not null ? Configuration as IConfigurationRoot : 已登记的单例`：
        // 主机上下文带了配置、但那份配置不是 IConfigurationRoot（被宿主换成某个 IConfigurationSection
        // 或自定义实现）时，as 得到 null，方法就此返回 null，也不再回落到已登记的 IConfiguration 单例，
        // GetConfiguration() 随之抛 XiHanException。本方法的返回类型本来就是 IConfiguration，
        // 没有"必须是根"这个要求，这里改成两级回落：主机上下文优先，其次已登记的单例。
        return hostBuilderContext?.Configuration ?? services.GetSingletonInstanceOrNull<IConfiguration>();
    }
}

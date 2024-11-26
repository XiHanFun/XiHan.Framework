#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionConfigurationExtensions
// Guid:16299546-c1f2-4b9a-811a-9b4c650137cd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:35:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
        return services.Replace(ServiceDescriptor.Singleton<IConfiguration>(configuration));
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
        return hostBuilderContext?.Configuration != null
            ? hostBuilderContext.Configuration as IConfigurationRoot
            : services.GetSingletonInstanceOrNull<IConfiguration>();
    }
}
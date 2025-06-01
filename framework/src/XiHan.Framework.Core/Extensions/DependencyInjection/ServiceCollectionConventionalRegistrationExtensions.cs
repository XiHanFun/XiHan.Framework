#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionConventionalRegistrationExtensions
// Guid:fcd633ec-bd19-4990-899a-0476e1f9c9cb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:26:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务集合常规注册扩展方法
/// </summary>
public static class ServiceCollectionConventionalRegistrationExtensions
{
    /// <summary>
    /// 添加常规注册器
    /// </summary>
    /// <param name="services"></param>
    /// <param name="registrar"></param>
    /// <returns></returns>
    public static IServiceCollection AddConventionalRegistrar(this IServiceCollection services, IConventionalRegistrar registrar)
    {
        GetOrCreateRegistrarList(services).Add(registrar);
        return services;
    }

    /// <summary>
    /// 获取常规注册器
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static List<IConventionalRegistrar> GetConventionalRegistrars(this IServiceCollection services)
    {
        return GetOrCreateRegistrarList(services);
    }

    /// <summary>
    /// 添加泛型程序集
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddAssemblyOf<T>(this IServiceCollection services)
    {
        return services.AddAssembly(typeof(T).GetTypeInfo().Assembly);
    }

    /// <summary>
    /// 添加特定程序集
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assembly"></param>
    /// <returns></returns>
    public static IServiceCollection AddAssembly(this IServiceCollection services, Assembly assembly)
    {
        foreach (var registrar in services.GetConventionalRegistrars())
        {
            registrar.AddAssembly(services, assembly);
        }

        return services;
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="services"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    public static IServiceCollection AddTypes(this IServiceCollection services, params Type[] types)
    {
        foreach (var registrar in services.GetConventionalRegistrars())
        {
            registrar.AddTypes(services, types);
        }

        return services;
    }

    /// <summary>
    /// 添加泛型类型
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddType<TType>(this IServiceCollection services)
    {
        return services.AddType(typeof(TType));
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IServiceCollection AddType(this IServiceCollection services, Type type)
    {
        foreach (var registrar in services.GetConventionalRegistrars())
        {
            registrar.AddType(services, type);
        }

        return services;
    }

    /// <summary>
    /// 获取或创建常规注册器
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    private static ConventionalRegistrarList GetOrCreateRegistrarList(IServiceCollection services)
    {
        var conventionalRegistrars = services.GetSingletonInstanceOrNull<IObjectAccessor<ConventionalRegistrarList>>()?.Value;
        if (conventionalRegistrars is not null)
        {
            return conventionalRegistrars;
        }

        conventionalRegistrars = [new DefaultConventionalRegistrar()];
        _ = services.AddObjectAccessor(conventionalRegistrars);

        return conventionalRegistrars;
    }
}

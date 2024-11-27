#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionCommonExtensions
// Guid:91eea845-f76c-4f48-9e76-eb3f9d6f50b7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:18:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Reflection;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务容器常用扩展方法
/// </summary>
public static class ServiceCollectionCommonExtensions
{
    /// <summary>
    /// 是否已添加
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static bool IsAdded<T>(this IServiceCollection services)
    {
        return services.IsAdded(typeof(T));
    }

    /// <summary>
    /// 是否已添加
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsAdded(this IServiceCollection services, Type type)
    {
        return services.Any(d => d.ServiceType == type);
    }

    /// <summary>
    /// 获取类型查找器
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static ITypeFinder GetTypeFinder(this IServiceCollection services)
    {
        return services.GetSingletonInstance<ITypeFinder>();
    }

    /// <summary>
    /// 查找并返回类型为 T 的服务的单一实例或空
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static T? GetSingletonInstanceOrNull<T>(this IServiceCollection services)
    {
        return (T?)services.FirstOrDefault(d => d.ServiceType == typeof(T))
            ?.NormalizedImplementationInstance();
    }

    /// <summary>
    /// 查找并返回类型为 T 的服务的单一实例
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static T GetSingletonInstance<T>(this IServiceCollection services)
    {
        var service = services.GetSingletonInstanceOrNull<T>();
        return service == null ? throw new InvalidOperationException($"找不到单例服务: {typeof(T).AssemblyQualifiedName}") : service;
    }

    /// <summary>
    /// 从工厂构建服务提供器
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceProvider BuildServiceProviderFromFactory(this IServiceCollection services)
    {
        _ = CheckHelper.NotNull(services, nameof(services));

        foreach (var service in services)
        {
            var factoryInterface = service.NormalizedImplementationInstance()?.GetType()
                .GetTypeInfo().GetInterfaces()
                .FirstOrDefault(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(IServiceProviderFactory<>));

            if (factoryInterface == null)
            {
                continue;
            }

            var containerBuilderType = factoryInterface.GenericTypeArguments[0];
            return (IServiceProvider)typeof(ServiceCollectionCommonExtensions).GetTypeInfo().GetMethods()
                .Single(m => m is { Name: nameof(BuildServiceProviderFromFactory), IsGenericMethod: true })
                .MakeGenericMethod(containerBuilderType)
                .Invoke(null, [services, null])!;
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 从工厂构建服务提供器
    /// </summary>
    /// <typeparam name="TContainerBuilder"></typeparam>
    /// <param name="services"></param>
    /// <param name="builderAction"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public static IServiceProvider BuildServiceProviderFromFactory<TContainerBuilder>(this IServiceCollection services, Action<TContainerBuilder>? builderAction = null) where TContainerBuilder : notnull
    {
        _ = CheckHelper.NotNull(services, nameof(services));

        var serviceProviderFactory = services.GetSingletonInstanceOrNull<IServiceProviderFactory<TContainerBuilder>>() ??
            throw new XiHanException($"在 {services} 中未发现服务提供器 {typeof(IServiceProviderFactory<TContainerBuilder>).FullName}");
        var builder = serviceProviderFactory.CreateBuilder(services);
        builderAction?.Invoke(builder);
        return serviceProviderFactory.CreateServiceProvider(builder);
    }

    /// <summary>
    /// 使用给定的服务容器解析依赖项
    /// 该方法只能在依赖注入注册阶段完成后使用
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    internal static T? GetService<T>(this IServiceCollection services)
    {
        return services.GetSingletonInstance<IXiHanApplication>().ServiceProvider.GetService<T>();
    }

    /// <summary>
    /// 使用给定的 <see cref="IServiceCollection"/>解析依赖项
    /// 该方法只能在依赖注入注册阶段完成后使用
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    internal static object? GetService(this IServiceCollection services, Type type)
    {
        return services.GetSingletonInstance<IXiHanApplication>().ServiceProvider.GetService(type);
    }

    /// <summary>
    /// 使用给定的<see cref="IServiceCollection"/>解析依赖项
    /// 如果未注册服务则抛出异常，该方法只能在依赖注入注册阶段完成后使用
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static T GetRequiredService<T>(this IServiceCollection services) where T : notnull
    {
        return services.GetSingletonInstance<IXiHanApplication>().ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// 使用给定的<see cref="IServiceCollection"/>解析依赖项
    /// 如果未注册服务则抛出异常，该方法只能在依赖注入注册阶段完成后使用
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static object GetRequiredService(this IServiceCollection services, Type type)
    {
        return services.GetSingletonInstance<IXiHanApplication>().ServiceProvider.GetRequiredService(type);
    }

    /// <summary>
    /// 返回一个<see cref="Lazy{T}"/>从给定的<see cref="IServiceCollection"/>解析服务
    /// 一旦依赖注入注册阶段完成
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static Lazy<T?> GetServiceLazy<T>(this IServiceCollection services)
    {
        return new Lazy<T?>(services.GetService<T>, true);
    }

    /// <summary>
    /// 返回一个<see cref="Lazy{T}"/>从给定的<see cref="IServiceCollection"/>解析服务
    /// 一旦依赖注入注册阶段完成
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Lazy<object?> GetServiceLazy(this IServiceCollection services, Type type)
    {
        return new Lazy<object?>(() => services.GetService(type), true);
    }

    /// <summary>
    /// 返回一个<see cref="Lazy{T}"/>从给定的<see cref="IServiceCollection"/>解析服务
    /// 一旦依赖注入注册阶段完成
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static Lazy<T> GetRequiredServiceLazy<T>(this IServiceCollection services) where T : notnull
    {
        return new Lazy<T>(services.GetRequiredService<T>, true);
    }

    /// <summary>
    /// 返回一个<see cref="Lazy{T}"/>从给定的<see cref="IServiceCollection"/>解析服务
    /// 一旦依赖注入注册阶段完成
    /// </summary>
    /// <param name="services"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Lazy<object> GetRequiredServiceLazy(this IServiceCollection services, Type type)
    {
        return new Lazy<object>(() => services.GetRequiredService(type), true);
    }

    /// <summary>
    /// 获取服务提供器或空
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceProvider? GetServiceProviderOrNull(this IServiceCollection services)
    {
        return services.GetObjectOrNull<IServiceProvider>();
    }
}
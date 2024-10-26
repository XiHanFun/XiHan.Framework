#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionObjectAccessorExtensions
// Guid:09cc3a1c-1eb6-44ec-b9b3-43bc9e22dc45
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:25:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务容器对象访问器扩展方法
/// </summary>
public static class ServiceCollectionObjectAccessorExtensions
{
    /// <summary>
    /// 尝试添加对象访问器，泛型
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static ObjectAccessor<T> TryAddObjectAccessor<T>(this IServiceCollection services)
    {
        // 若已存在，则直接返回单例实例
        if (services.Any(s => s.ServiceType == typeof(ObjectAccessor<T>)))
        {
            return services.GetSingletonInstance<ObjectAccessor<T>>();
        }

        return services.AddObjectAccessor<T>();
    }

    /// <summary>
    /// 添加对象访问器，泛型
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static ObjectAccessor<T> AddObjectAccessor<T>(this IServiceCollection services)
    {
        return services.AddObjectAccessor(new ObjectAccessor<T>());
    }

    /// <summary>
    /// 添加对象访问器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static ObjectAccessor<T> AddObjectAccessor<T>(this IServiceCollection services, T obj)
    {
        return services.AddObjectAccessor(new ObjectAccessor<T>(obj));
    }

    /// <summary>
    /// 添加对象访问器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <param name="accessor"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static ObjectAccessor<T> AddObjectAccessor<T>(this IServiceCollection services, ObjectAccessor<T> accessor)
    {
        if (services.Any(s => s.ServiceType == typeof(ObjectAccessor<T>)))
        {
            throw new Exception($"对象访问器是在type之前注册的:{typeof(T).AssemblyQualifiedName}");
        }

        // 添加到开头，以便快速检索
        services.Insert(0, ServiceDescriptor.Singleton(typeof(ObjectAccessor<T>), accessor));
        services.Insert(0, ServiceDescriptor.Singleton(typeof(IObjectAccessor<T>), accessor));

        return accessor;
    }

    /// <summary>
    /// 获取对象单一实例，没有返回空
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static T? GetObjectOrNull<T>(this IServiceCollection services)
        where T : class
    {
        return services.GetSingletonInstanceOrNull<IObjectAccessor<T>>()?.Value;
    }

    /// <summary>
    /// 获取对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static T GetObject<T>(this IServiceCollection services)
        where T : class
    {
        return services.GetObjectOrNull<T>() ?? throw new Exception($"找不到{typeof(T).AssemblyQualifiedName}。确保已通过 AddObjectAccessor 添加!");
    }
}
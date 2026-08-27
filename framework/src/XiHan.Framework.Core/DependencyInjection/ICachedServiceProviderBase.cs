// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 缓存服务提供程序基类接口
/// </summary>
public interface ICachedServiceProviderBase : IKeyedServiceProvider
{
    /// <summary>
    /// 获取服务（重新声明 <see cref="IServiceProvider.GetService"/>）
    /// </summary>
    /// <remarks>
    /// 这条声明不能删。C# 在方法组解析时会先做「只保留最派生类型的成员」这一步：本接口自己声明了若干
    /// GetService 重载，于是基接口 <see cref="IServiceProvider"/> 上的 GetService(Type) 会被整条剔除出候选集。
    /// 结果是通过 <see cref="ICachedServiceProvider"/> / <see cref="ITransientCachedServiceProvider"/> 这类
    /// 接口引用写 <c>provider.GetService(typeof(Foo))</c> 时，唯一还能匹配的候选变成 <c>GetService&lt;T&gt;(T defaultValue)</c>
    /// （T 被推断为 Type），实际执行的是「去解析 typeof(Type) 这个服务，解析不到就把传进来的那个 Type 当默认值返回」，
    /// 既不报错也拿不到目标服务——静默返回错误对象。通过实现类引用调用则没有这个问题，因为那时所有重载都声明在同一个类上，
    /// 重载决议会按「非泛型优于泛型」正确选中 GetService(Type)。
    /// 在本接口上原样重新声明，可把它拉回同一层候选集，恢复直觉绑定；实现方无需任何改动，因为
    /// <see cref="IServiceProvider"/> 本来就要求实现该成员。
    /// </remarks>
    /// <param name="serviceType">服务类型</param>
    /// <returns>服务实例，未注册时为空</returns>
    new object? GetService(Type serviceType);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    T GetService<T>(T defaultValue);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    object GetService(Type serviceType, object defaultValue);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="factory"></param>
    /// <returns></returns>
    T GetService<T>(Func<IServiceProvider, object> factory);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    object GetService(Type serviceType, Func<IServiceProvider, object> factory);
}

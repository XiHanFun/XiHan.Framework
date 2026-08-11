// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务实现类型登记表
/// </summary>
/// <remarks>
/// 记录以工厂委托注册的服务描述器与其实现类型的对应关系。
/// <see cref="ServiceDescriptor"/> 以工厂委托注册时其实现类型为空，
/// 需要实现类型的环节（如动态代理）经本表按描述器实例反查。
/// </remarks>
public class ServiceImplementationTypeRegistry
{
    private readonly Dictionary<ServiceDescriptor, Type> _implementationTypes = [];

    /// <summary>
    /// 读取服务描述器自身声明的实现类型，区分键值服务与非键值服务
    /// </summary>
    /// <param name="descriptor">服务描述器</param>
    /// <returns>描述器声明的实现类型，以工厂或实例注册时为空</returns>
    public static Type? GetDeclaredImplementationTypeOrNull(ServiceDescriptor descriptor)
    {
        return descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType;
    }

    /// <summary>
    /// 登记服务描述器的实现类型
    /// </summary>
    /// <param name="descriptor">服务描述器</param>
    /// <param name="implementationType">实现类型</param>
    public void Add(ServiceDescriptor descriptor, Type implementationType)
    {
        _implementationTypes[descriptor] = implementationType;
    }

    /// <summary>
    /// 获取登记表中的实现类型，未登记时返回空
    /// </summary>
    /// <param name="descriptor">服务描述器</param>
    /// <returns>实现类型，未登记时为空</returns>
    public Type? GetOrNull(ServiceDescriptor descriptor)
    {
        return _implementationTypes.GetValueOrDefault(descriptor);
    }

    /// <summary>
    /// 获取服务描述器的实现类型，优先取描述器自身声明的，其次查登记表
    /// </summary>
    /// <param name="descriptor">服务描述器</param>
    /// <returns>实现类型，两者皆无时为空</returns>
    public Type? ResolveImplementationTypeOrNull(ServiceDescriptor descriptor)
    {
        return GetDeclaredImplementationTypeOrNull(descriptor) ?? GetOrNull(descriptor);
    }
}

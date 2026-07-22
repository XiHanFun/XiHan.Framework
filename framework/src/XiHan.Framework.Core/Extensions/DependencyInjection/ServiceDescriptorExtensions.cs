// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务描述器扩展方法
/// 包括该服务的类型、实现和生存期
/// </summary>
public static class ServiceDescriptorExtensions
{
    /// <summary>
    /// 规范化键控服务和非键控服务之间的实现实例数据
    /// </summary>
    /// <param name="descriptor">
    /// 规范化<see cref="ServiceDescriptor"/>
    /// </param>
    /// <returns>
    /// 来自服务描述器的适当实现类型
    /// </returns>
    public static object? NormalizedImplementationInstance(this ServiceDescriptor descriptor)
    {
        return descriptor.IsKeyedService ? descriptor.KeyedImplementationInstance : descriptor.ImplementationInstance;
    }

    /// <summary>
    /// 规范化键控和非键控服务之间的实现类型数据
    /// </summary>
    /// <param name="descriptor">
    /// 规范化<see cref="ServiceDescriptor"/>
    /// </param>
    /// <returns>
    /// 来自服务描述器的适当实现类型
    /// </returns>
    public static Type? NormalizedImplementationType(this ServiceDescriptor descriptor)
    {
        return descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType;
    }
}

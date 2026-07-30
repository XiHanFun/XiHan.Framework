// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务暴露时上下文
/// </summary>
public class OnServiceExposingContext : IOnServiceExposingContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="implementationType"></param>
    /// <param name="exposedTypes"></param>
    public OnServiceExposingContext(Type implementationType, List<Type> exposedTypes)
    {
        ImplementationType = Guard.NotNull(implementationType, nameof(implementationType));
        ExposedTypes = Guard.NotNull(exposedTypes, nameof(exposedTypes)).ConvertAll(t => new ServiceIdentifier(t));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="implementationType"></param>
    /// <param name="exposedTypes"></param>
    public OnServiceExposingContext(Type implementationType, List<ServiceIdentifier> exposedTypes)
    {
        ImplementationType = Guard.NotNull(implementationType, nameof(implementationType));
        ExposedTypes = Guard.NotNull(exposedTypes, nameof(exposedTypes));
    }

    /// <summary>
    /// 实现类型
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// 暴露的类型
    /// </summary>
    public List<ServiceIdentifier> ExposedTypes { get; }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务注册时上下文
/// </summary>
public class OnServiceRegistredContext : IOnServiceRegistredContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="implementationType"></param>
    public OnServiceRegistredContext(Type serviceType, Type implementationType)
    {
        ServiceType = Guard.NotNull(serviceType, nameof(serviceType));
        ImplementationType = Guard.NotNull(implementationType, nameof(implementationType));

        Interceptors = new TypeList<IXiHanInterceptor>();
    }

    /// <summary>
    /// 拦截器
    /// </summary>
    public virtual ITypeList<IXiHanInterceptor> Interceptors { get; }

    /// <summary>
    /// 服务类型
    /// </summary>
    public virtual Type ServiceType { get; }

    /// <summary>
    /// 实现类型
    /// </summary>
    public virtual Type ImplementationType { get; }
}

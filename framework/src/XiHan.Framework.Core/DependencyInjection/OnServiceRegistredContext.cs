#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OnServiceRegistredContext
// Guid:ff581f06-ecca-4a64-b29e-e9edd2ee5d56
// Author:Administrator
// Email:me@zhaifanhua.com
// CreateTime:2024-04-25 上午 10:46:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.DynamicProxy;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务注册时上下文
/// </summary>
public class OnServiceRegistredContext : IOnServiceRegistredContext
{
    /// <summary>
    /// 拦截器
    /// </summary>
    public virtual ITypeList<IInterceptor> Interceptors { get; }

    /// <summary>
    /// 服务类型
    /// </summary>
    public virtual Type ServiceType { get; }

    /// <summary>
    /// 实现类型
    /// </summary>
    public virtual Type ImplementationType { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceType"></param>
    /// <param name="implementationType"></param>
    public OnServiceRegistredContext(Type serviceType, Type implementationType)
    {
        ServiceType = CheckHelper.NotNull(serviceType, nameof(serviceType));
        ImplementationType = CheckHelper.NotNull(implementationType, nameof(implementationType));

        Interceptors = new TypeList<IInterceptor>();
    }
}
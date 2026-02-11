#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkInterceptorRegistrar
// Guid:940a5d2d-e054-4a1e-8781-7e141db6a691
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/01 20:06:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元拦截器注册器
/// </summary>
public static class UnitOfWorkInterceptorRegistrar
{
    /// <summary>
    /// 注册需要忽略的动态代理类型
    /// </summary>
    /// <param name="context"></param>
    public static void RegisterIfNeeded(IOnServiceRegistredContext context)
    {
        if (ShouldIntercept(context.ImplementationType))
        {
            context.Interceptors.TryAdd<UnitOfWorkInterceptor>();
        }
    }

    /// <summary>
    /// 需要忽略的动态代理类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static bool ShouldIntercept(Type type)
    {
        return !DynamicProxyIgnoreTypes.Contains(type) && UnitOfWorkHelper.IsUnitOfWorkType(type.GetTypeInfo());
    }
}

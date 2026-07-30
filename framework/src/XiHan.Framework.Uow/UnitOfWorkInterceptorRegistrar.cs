// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

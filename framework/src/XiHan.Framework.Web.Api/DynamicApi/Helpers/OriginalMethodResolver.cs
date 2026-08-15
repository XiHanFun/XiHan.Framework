// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace XiHan.Framework.Web.Api.DynamicApi.Helpers;

/// <summary>
/// 动态控制器动作到原始服务方法的解析器
/// </summary>
/// <remarks>
/// 动态控制器的动作方法由 IL 发射生成，只是对应用服务方法的一层转发，
/// 应用服务方法上的特性（如 <c>[UnitOfWork]</c>）不会出现在动作方法上，
/// 需要经 <see cref="OriginalMethodAttribute"/> 回查原始方法后再读取。
/// </remarks>
public static class OriginalMethodResolver
{
    private static readonly ConcurrentDictionary<MethodInfo, MethodInfo> ResolvedMethodCache = new();

    /// <summary>
    /// 解析动作方法背后的原始服务方法，非动态控制器或回查失败时返回动作方法本身
    /// </summary>
    /// <param name="actionMethod">控制器动作方法</param>
    /// <returns>用于读取业务特性的方法</returns>
    public static MethodInfo Resolve(MethodInfo actionMethod)
    {
        ArgumentNullException.ThrowIfNull(actionMethod);

        return ResolvedMethodCache.GetOrAdd(actionMethod, ResolveCore);
    }

    private static MethodInfo ResolveCore(MethodInfo actionMethod)
    {
        var originalMethodAttribute = actionMethod.GetCustomAttribute<OriginalMethodAttribute>();
        if (originalMethodAttribute is null)
        {
            return actionMethod;
        }

        var originalMethod = originalMethodAttribute.ServiceType.GetMethod(
            originalMethodAttribute.MethodName,
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: originalMethodAttribute.ParameterTypes,
            modifiers: null);

        return originalMethod ?? actionMethod;
    }
}

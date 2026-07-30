// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 集成服务特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class IntegrationServiceAttribute : Attribute
{
    /// <summary>
    /// 是否已定义或已继承
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool IsDefinedOrInherited<T>()
    {
        return IsDefinedOrInherited(typeof(T));
    }

    /// <summary>
    /// 是否已定义或已继承
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsDefinedOrInherited(Type type)
    {
        return type.IsDefined(typeof(IntegrationServiceAttribute), true) || type.GetInterfaces().Any(@interface => @interface.IsDefined(typeof(IntegrationServiceAttribute), true));
    }
}

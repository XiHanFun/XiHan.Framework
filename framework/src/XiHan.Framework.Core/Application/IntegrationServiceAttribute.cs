#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IntegrationServiceAttribute
// Guid:82a39e07-df4b-4229-ae5f-660e00c5caeb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 3:38:54
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

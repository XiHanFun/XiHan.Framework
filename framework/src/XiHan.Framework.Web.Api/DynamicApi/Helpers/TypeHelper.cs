#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TypeHelper
// Guid:d587549b-98e4-4147-a4c9-baaebf6fc299
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Application.Contracts.Services;

namespace XiHan.Framework.Web.Api.DynamicApi.Helpers;

/// <summary>
/// 类型辅助类
/// </summary>
public static class TypeHelper
{
    /// <summary>
    /// 判断是否是应用服务
    /// </summary>
    public static bool IsApplicationService(Type type)
    {
        return type.IsClass &&
               !type.IsAbstract &&
               type.IsPublic &&
               typeof(IApplicationService).IsAssignableFrom(type);
    }

    /// <summary>
    /// 判断方法是否继承自 Object
    /// </summary>
    public static bool IsInheritedFromObject(MethodInfo method)
    {
        return method.DeclaringType == typeof(object) ||
               (method.DeclaringType == typeof(ValueType) && method.Name == "GetHashCode");
    }

    /// <summary>
    /// 判断方法是否应该被暴露为 API
    /// </summary>
    public static bool ShouldExposeAsApi(MethodInfo method)
    {
        return method.IsPublic &&
               !method.IsSpecialName &&
               !IsInheritedFromObject(method) &&
               method.DeclaringType != null &&
               method.DeclaringType != typeof(object);
    }

    /// <summary>
    /// 获取方法的友好名称
    /// </summary>
    public static string GetFriendlyMethodName(MethodInfo method)
    {
        var name = method.Name;

        // 移除 Async 后缀
        if (name.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^5];
        }

        return name;
    }

    /// <summary>
    /// 获取类型的友好名称
    /// </summary>
    public static string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeName = type.Name[..type.Name.IndexOf('`')];
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{genericTypeName}<{genericArgs}>";
        }

        return type.Name;
    }
}

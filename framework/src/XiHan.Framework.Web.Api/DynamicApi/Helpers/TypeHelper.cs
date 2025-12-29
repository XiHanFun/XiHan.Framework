#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TypeHelper
// Guid:type-helper-dynamic-api-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Application.Services;

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
    /// 判断是否是简单类型
    /// </summary>
    public static bool IsSimpleType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // 基本类型检查
        if (underlyingType.IsPrimitive ||
            underlyingType.IsEnum ||
            underlyingType == typeof(string) ||
            underlyingType == typeof(decimal) ||
            underlyingType == typeof(DateTime) ||
            underlyingType == typeof(DateTimeOffset) ||
            underlyingType == typeof(TimeSpan) ||
            underlyingType == typeof(Guid) ||
            underlyingType == typeof(DateOnly) ||
            underlyingType == typeof(TimeOnly))
        {
            return true;
        }

        // 检查是否是值类型且有到基本类型的隐式转换运算符
        // 这样可以支持 ID 包装类型（如 struct）
        if (underlyingType.IsValueType && !underlyingType.IsGenericType)
        {
            // 检查是否有 implicit operator 到基本类型的转换
            var methods = underlyingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name == "op_Implicit" && method.ReturnType.IsPrimitive || method.ReturnType == typeof(string))
                {
                    return true;
                }
            }
        }

        return false;
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

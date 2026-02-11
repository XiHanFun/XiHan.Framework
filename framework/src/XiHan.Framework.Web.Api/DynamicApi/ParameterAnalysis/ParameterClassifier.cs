#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParameterClassifier
// Guid:c373e619-d2b4-4009-ab5f-3bfd79dbd3f0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Reflection;
using System.Security.Claims;

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数分类器
/// </summary>
public static class ParameterClassifier
{
    /// <summary>
    /// 分类参数种类
    /// </summary>
    public static ParameterKind ClassifyKind(Type type)
    {
        // 特殊类型
        if (IsSpecialType(type))
        {
            return ParameterKind.Special;
        }

        // 集合类型
        if (IsCollectionType(type))
        {
            return ParameterKind.Collection;
        }

        // 简单类型
        if (IsSimpleType(type))
        {
            return ParameterKind.Simple;
        }

        // 复杂类型
        return ParameterKind.Complex;
    }

    /// <summary>
    /// 分类参数角色
    /// </summary>
    public static ParameterRole ClassifyRole(ParameterInfo parameter, string methodName)
    {
        var type = parameter.ParameterType;
        var name = parameter.Name ?? string.Empty;

        // 基础设施参数
        if (IsSpecialType(type))
        {
            return ParameterRole.Infra;
        }

        // 主键参数
        if (IsIdParameter(name, type))
        {
            return ParameterRole.Id;
        }

        // 批量操作
        if (IsCollectionType(type))
        {
            return ParameterRole.Batch;
        }

        // 命令参数（Create / Update DTO）
        if (IsCommandParameter(methodName, type))
        {
            return ParameterRole.Command;
        }

        // 查询参数
        return ParameterRole.Query;
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
        if (underlyingType.IsValueType && !underlyingType.IsGenericType)
        {
            var methods = underlyingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name == "op_Implicit" &&
                    (method.ReturnType.IsPrimitive || method.ReturnType == typeof(string)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断是否是集合类型
    /// </summary>
    public static bool IsCollectionType(Type type)
    {
        return type != typeof(string) &&
               typeof(IEnumerable).IsAssignableFrom(type);
    }

    /// <summary>
    /// 判断是否是特殊类型（基础设施参数）
    /// </summary>
    public static bool IsSpecialType(Type type)
    {
        return type == typeof(CancellationToken) ||
               type == typeof(HttpContext) ||
               type == typeof(ClaimsPrincipal) ||
               type == typeof(IServiceProvider);
    }

    /// <summary>
    /// 判断是否是 Id 参数
    /// </summary>
    /// <remarks>
    /// 识别规则：
    /// - 参数名 == "id"（忽略大小写）
    /// - 参数名以 "Id" 或 "ID" 结尾
    /// - 类型是 long / int / Guid / string
    /// </remarks>
    public static bool IsIdParameter(string parameterName, Type type)
    {
        // 检查参数名
        var isIdName = parameterName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                       parameterName.EndsWith("Id", StringComparison.Ordinal) ||
                       parameterName.EndsWith("ID", StringComparison.Ordinal);

        if (!isIdName)
        {
            return false;
        }

        // 检查类型（允许 Id 类型）
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(Guid) ||
               underlyingType == typeof(string) ||
               underlyingType.IsValueType; // 支持自定义 Id 类型（struct）
    }

    /// <summary>
    /// 判断是否是命令参数
    /// </summary>
    private static bool IsCommandParameter(string methodName, Type type)
    {
        // 复杂类型 + Create/Update/Add/Modify 方法
        if (!IsSimpleType(type) && !IsCollectionType(type))
        {
            var lowerMethodName = methodName.ToLowerInvariant();
            return lowerMethodName.Contains("create") ||
                   lowerMethodName.Contains("update") ||
                   lowerMethodName.Contains("add") ||
                   lowerMethodName.Contains("modify") ||
                   lowerMethodName.Contains("edit");
        }

        return false;
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

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
    /// 判断类型是否包含表单文件。
    /// </summary>
    public static bool ContainsFormFile(Type type)
    {
        return ContainsFormFile(type, []);
    }

    private static bool ContainsFormFile(Type type, HashSet<Type> visitedTypes)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(IFormFile) ||
            underlyingType == typeof(IFormFileCollection) ||
            typeof(IEnumerable<IFormFile>).IsAssignableFrom(underlyingType))
        {
            return true;
        }

        if (underlyingType.IsArray)
        {
            var elementType = underlyingType.GetElementType();
            return elementType != null && ContainsFormFile(elementType, visitedTypes);
        }

        if (!visitedTypes.Add(underlyingType))
        {
            return false;
        }

        if (GetEnumerableElementTypes(underlyingType).Any(elementType => ContainsFormFile(elementType, visitedTypes)))
        {
            return true;
        }

        if (IsSimpleType(underlyingType) || IsSpecialType(underlyingType))
        {
            return false;
        }

        if (underlyingType.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            return false;
        }

        return underlyingType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Any(property => ContainsFormFile(property.PropertyType, visitedTypes));
    }

    private static IEnumerable<Type> GetEnumerableElementTypes(Type type)
    {
        if (type == typeof(string))
        {
            yield break;
        }

        var candidates = type.IsInterface
            ? type.GetInterfaces().Prepend(type)
            : type.GetInterfaces();

        foreach (var candidate in candidates)
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                yield return candidate.GetGenericArguments()[0];
            }
        }
    }

    /// <summary>
    /// 判断是否是 Id 参数
    /// </summary>
    /// <remarks>
    /// 识别规则：
    /// - 参数名 == "id"（忽略大小写）
    /// - 参数名以 "Id" 或 "ID" 结尾
    /// - 类型是常见标识类型（long / int / Guid / string 等）或可解析的自定义 Id 值对象
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

        return IsSupportedIdType(underlyingType);
    }

    /// <summary>
    /// 判断是否是可接受的 Id 类型
    /// </summary>
    private static bool IsSupportedIdType(Type underlyingType)
    {
        // 常见标识类型
        if (underlyingType == typeof(string) ||
            underlyingType == typeof(Guid) ||
            underlyingType == typeof(int) ||
            underlyingType == typeof(long) ||
            underlyingType == typeof(short) ||
            underlyingType == typeof(byte) ||
            underlyingType == typeof(uint) ||
            underlyingType == typeof(ulong) ||
            underlyingType == typeof(ushort) ||
            underlyingType == typeof(sbyte))
        {
            return true;
        }

        // 明确排除易误判类型
        if (underlyingType.IsEnum ||
            underlyingType == typeof(bool) ||
            underlyingType == typeof(char) ||
            underlyingType == typeof(float) ||
            underlyingType == typeof(double) ||
            underlyingType == typeof(decimal) ||
            underlyingType == typeof(DateTime) ||
            underlyingType == typeof(DateTimeOffset) ||
            underlyingType == typeof(TimeSpan) ||
            underlyingType == typeof(DateOnly) ||
            underlyingType == typeof(TimeOnly))
        {
            return false;
        }

        // 支持自定义 Id 值对象（结构体）：
        // 1. 类型名以 Id 结尾，或
        // 2. 实现 IParsable<TSelf>，可从路由字符串解析
        if (underlyingType.IsValueType && !underlyingType.IsPrimitive)
        {
            if (underlyingType.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var item in underlyingType.GetInterfaces())
            {
                if (!item.IsGenericType)
                {
                    continue;
                }

                if (item.GetGenericTypeDefinition() == typeof(IParsable<>) &&
                    item.GetGenericArguments()[0] == underlyingType)
                {
                    return true;
                }
            }
        }

        return false;
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

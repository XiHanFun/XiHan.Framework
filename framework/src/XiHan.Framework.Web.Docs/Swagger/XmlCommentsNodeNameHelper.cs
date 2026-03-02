#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XmlCommentsNodeNameHelper
// Guid:7abd152d-44da-4b77-a8b0-55b97b692bf9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 16:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Web.Docs.Swagger;

/// <summary>
/// XML 注释节点名称辅助类
/// </summary>
internal static class XmlCommentsNodeNameHelper
{
    /// <summary>
    /// 获取方法的成员名称（首个候选）
    /// </summary>
    public static string GetMemberNameForMethod(MethodInfo method)
    {
        return GetMemberNamesForMethod(method).FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// 获取方法的成员名称候选（用于兼容泛型基类方法）
    /// </summary>
    public static IReadOnlyList<string> GetMemberNamesForMethod(MethodInfo method)
    {
        var memberNames = new List<string>();

        AddMethodMemberName(memberNames, method);
        AddMethodMemberName(memberNames, TryGetGenericTypeDefinitionMethod(method));

        var baseDefinition = method.GetBaseDefinition();
        if (baseDefinition != method)
        {
            AddMethodMemberName(memberNames, baseDefinition);
            AddMethodMemberName(memberNames, TryGetGenericTypeDefinitionMethod(baseDefinition));
        }

        return memberNames;
    }

    /// <summary>
    /// 获取类型成员名称
    /// </summary>
    public static string GetMemberNameForType(Type type)
    {
        if (type.IsConstructedGenericType)
        {
            type = type.GetGenericTypeDefinition();
        }

        var typeName = (type.FullName ?? type.Name).Replace("+", ".");
        return $"T:{typeName}";
    }

    private static void AddMethodMemberName(ICollection<string> memberNames, MethodInfo? method)
    {
        if (method == null)
        {
            return;
        }

        var memberName = BuildMethodMemberName(method);
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return;
        }

        if (!memberNames.Contains(memberName))
        {
            memberNames.Add(memberName);
        }
    }

    private static string BuildMethodMemberName(MethodInfo method)
    {
        var declaringTypeName = GetDeclaringTypeName(method.DeclaringType);
        if (string.IsNullOrWhiteSpace(declaringTypeName))
        {
            return string.Empty;
        }

        var parameterList = method.GetParameters();
        var parameterTypeNames = parameterList
            .Select(parameter => GetParameterTypeName(parameter.ParameterType))
            .ToArray();

        var parameters = parameterTypeNames.Length > 0
            ? $"({string.Join(",", parameterTypeNames)})"
            : string.Empty;

        return $"M:{declaringTypeName}.{method.Name}{parameters}";
    }

    private static string GetDeclaringTypeName(Type? type)
    {
        if (type == null)
        {
            return string.Empty;
        }

        if (type.IsConstructedGenericType)
        {
            type = type.GetGenericTypeDefinition();
        }

        return (type.FullName ?? type.Name).Replace("+", ".");
    }

    private static string GetParameterTypeName(Type parameterType)
    {
        if (parameterType.IsByRef)
        {
            return $"{GetParameterTypeName(parameterType.GetElementType()!)}@";
        }

        if (parameterType.IsPointer)
        {
            return $"{GetParameterTypeName(parameterType.GetElementType()!)}*";
        }

        if (parameterType.IsArray)
        {
            var rankSuffix = parameterType.GetArrayRank() > 1
                ? new string(',', parameterType.GetArrayRank() - 1)
                : string.Empty;
            return $"{GetParameterTypeName(parameterType.GetElementType()!)}[{rankSuffix}]";
        }

        if (parameterType.IsGenericParameter)
        {
            return parameterType.DeclaringMethod != null
                ? $"``{parameterType.GenericParameterPosition}"
                : $"`{parameterType.GenericParameterPosition}";
        }

        if (parameterType.IsGenericType)
        {
            var genericTypeDefinition = parameterType.GetGenericTypeDefinition();
            var genericTypeName = (genericTypeDefinition.FullName ?? genericTypeDefinition.Name).Replace("+", ".");
            var backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex >= 0)
            {
                genericTypeName = genericTypeName[..backtickIndex];
            }

            var genericArgs = parameterType.GetGenericArguments()
                .Select(GetParameterTypeName);
            return $"{genericTypeName}{{{string.Join(",", genericArgs)}}}";
        }

        return (parameterType.FullName ?? parameterType.Name).Replace("+", ".");
    }

    private static MethodInfo? TryGetGenericTypeDefinitionMethod(MethodInfo method)
    {
        var declaringType = method.DeclaringType;
        if (declaringType == null || !declaringType.IsConstructedGenericType)
        {
            return null;
        }

        var genericTypeDefinition = declaringType.GetGenericTypeDefinition();
        var candidates = genericTypeDefinition
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(candidate => candidate.Name == method.Name)
            .Where(candidate => candidate.GetParameters().Length == method.GetParameters().Length)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        var matchedByParameterName = candidates.FirstOrDefault(candidate =>
        {
            var left = candidate.GetParameters();
            var right = method.GetParameters();
            for (var i = 0; i < left.Length; i++)
            {
                if (!string.Equals(left[i].Name, right[i].Name, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        });

        return matchedByParameterName ?? candidates[0];
    }
}

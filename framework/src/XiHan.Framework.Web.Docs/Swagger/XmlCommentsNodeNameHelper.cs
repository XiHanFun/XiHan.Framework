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
    /// 获取方法的成员名称
    /// </summary>
    public static string GetMemberNameForMethod(MethodInfo method)
    {
        var declaringTypeName = method.DeclaringType?.FullName?.Replace("+", ".");
        var parameters = method.GetParameters();

        var parameterTypeNames = parameters.Select(p =>
        {
            var typeName = p.ParameterType.FullName ?? p.ParameterType.Name;
            // 处理泛型参数
            if (p.ParameterType.IsGenericType)
            {
                var genericTypeName = typeName.Substring(0, typeName.IndexOf('`'));
                var genericArgs = p.ParameterType.GetGenericArguments();
                var genericArgNames = string.Join(",", genericArgs.Select(t => t.FullName ?? t.Name));
                typeName = $"{genericTypeName}{{{genericArgNames}}}";
            }
            return typeName.Replace("+", ".");
        });

        var parameterList = parameters.Length > 0
            ? $"({string.Join(",", parameterTypeNames)})"
            : string.Empty;

        return $"M:{declaringTypeName}.{method.Name}{parameterList}";
    }
}

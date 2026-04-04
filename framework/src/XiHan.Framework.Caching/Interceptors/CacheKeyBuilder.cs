#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheKeyBuilder
// Guid:a7b8c9d0-e1f2-3456-0123-456789abcdef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Text.RegularExpressions;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Caching.Interceptors;

/// <summary>
/// 缓存键构建器，将键模板中的 {paramName} 占位符替换为实际方法参数值
/// </summary>
public static partial class CacheKeyBuilder
{
    /// <summary>
    /// 根据键模板和方法调用上下文构建缓存键
    /// </summary>
    /// <param name="template">键模板，如 "config:{tenantId}:{key}"</param>
    /// <param name="invocation">方法调用上下文</param>
    /// <returns>构建好的缓存键</returns>
    public static string Build(string template, IXiHanMethodInvocation invocation)
    {
        var parameters = invocation.Method.GetParameters();
        var arguments = invocation.Arguments;

        var result = template;

        for (var i = 0; i < parameters.Length; i++)
        {
            var paramName = parameters[i].Name!;
            var paramValue = arguments[i]?.ToString() ?? "null";
            result = result.Replace($"{{{paramName}}}", paramValue);
        }

        return result;
    }

    /// <summary>
    /// 判断键模板是否包含占位符
    /// </summary>
    /// <param name="template">键模板</param>
    /// <returns>是否包含占位符</returns>
    public static bool HasPlaceholders(string template)
    {
        return PlaceholderPattern().IsMatch(template);
    }

    [GeneratedRegex(@"\{[a-zA-Z_]\w*\}")]
    private static partial Regex PlaceholderPattern();
}

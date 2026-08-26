// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
        return Build(template, invocation.Method, invocation.Arguments);
    }

    /// <summary>
    /// 根据键模板、方法与实参构建缓存键
    /// </summary>
    /// <param name="template">键模板，如 "config:{tenantId}:{key}"</param>
    /// <param name="method">方法，占位符按其形参名匹配</param>
    /// <param name="arguments">与形参一一对应的实参，短于形参列表时缺位按 null 处理</param>
    /// <returns>构建好的缓存键</returns>
    public static string Build(string template, MethodInfo method, IReadOnlyList<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(arguments);

        var parameters = method.GetParameters();
        var result = template;

        for (var i = 0; i < parameters.Length; i++)
        {
            var paramName = parameters[i].Name!;
            var paramValue = (i < arguments.Count ? arguments[i]?.ToString() : null) ?? "null";
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

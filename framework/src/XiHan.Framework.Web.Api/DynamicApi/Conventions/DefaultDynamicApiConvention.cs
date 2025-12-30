#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultDynamicApiConvention
// Guid:h8i9j0k1-l2m3-4n4o-5p6q-7r8s9t0u1v2w
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using System.Text.RegularExpressions;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Configuration;

namespace XiHan.Framework.Web.Api.DynamicApi.Conventions;

/// <summary>
/// 默认动态 API 约定
/// </summary>
public class DefaultDynamicApiConvention : IDynamicApiConvention
{
    private readonly DynamicApiOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">配置选项</param>
    public DefaultDynamicApiConvention(DynamicApiOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// 应用约定
    /// </summary>
    /// <param name="context">约定上下文</param>
    public void Apply(DynamicApiConventionContext context)
    {
        // 检查是否禁用动态 API
        if (IsDisabled(context.ServiceType, context.MethodInfo))
        {
            context.IsEnabled = false;
            return;
        }

        // 设置控制器名称
        if (string.IsNullOrEmpty(context.ControllerName))
        {
            context.ControllerName = GetControllerName(context.ServiceType);
        }

        // 如果没有方法信息，只需要控制器名称
        if (context.MethodInfo == null)
        {
            return;
        }

        // 设置动作名称
        if (string.IsNullOrEmpty(context.ActionName))
        {
            context.ActionName = GetActionName(context.MethodInfo);
        }

        // 设置 HTTP 方法
        if (string.IsNullOrEmpty(context.HttpMethod))
        {
            context.HttpMethod = GetHttpMethod(context.MethodInfo);
        }

        // 设置路由模板
        if (string.IsNullOrEmpty(context.RouteTemplate))
        {
            context.RouteTemplate = GetRouteTemplate(context);
        }

        // 设置 API 版本
        if (string.IsNullOrEmpty(context.ApiVersion))
        {
            context.ApiVersion = GetApiVersion(context.ServiceType) ?? _options.DefaultApiVersion;
        }
    }

    /// <summary>
    /// 检查是否禁用动态 API
    /// </summary>
    private static bool IsDisabled(Type serviceType, MethodInfo? methodInfo)
    {
        // 检查类级别的动态 API 特性
        var classAttribute = serviceType.GetCustomAttribute<DynamicApiAttribute>();
        if (classAttribute != null && !classAttribute.IsEnabled)
        {
            return true;
        }

        // 检查方法级别的禁用标记
        if (methodInfo != null)
        {
            var methodAttribute = methodInfo.GetCustomAttribute<DynamicApiAttribute>();
            if (methodAttribute != null && !methodAttribute.IsEnabled)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断是否是简单类型
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// 获取 API 版本
    /// </summary>
    private static string? GetApiVersion(Type serviceType)
    {
        var attribute = serviceType.GetCustomAttribute<DynamicApiAttribute>();
        return attribute?.Version;
    }

    /// <summary>
    /// 获取路由参数
    /// </summary>
    private static List<string> GetRouteParameters(MethodInfo methodInfo, string? httpMethod)
    {
        var parameters = new List<string>();
        var methodParameters = methodInfo.GetParameters();

        foreach (var parameter in methodParameters)
        {
            // 对于 GET 和 DELETE 请求，将简单类型参数添加到路由
            if (httpMethod is "GET" or "DELETE")
            {
                if (IsSimpleType(parameter.ParameterType))
                {
                    parameters.Add($"{{{parameter.Name}}}");
                }
            }
            // 对于 PUT 和 PATCH 请求，通常第一个参数是 ID
            else if (httpMethod is "PUT" or "PATCH")
            {
                if (IsSimpleType(parameter.ParameterType) && parameters.Count == 0)
                {
                    parameters.Add($"{{{parameter.Name}}}");
                }
            }
        }

        return parameters;
    }

    /// <summary>
    /// 获取控制器名称
    /// </summary>
    private string GetControllerName(Type serviceType)
    {
        var name = serviceType.Name;

        // 移除服务后缀
        if (_options.RemoveServiceSuffix)
        {
            foreach (var suffix in _options.ServiceSuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^suffix.Length];
                    break;
                }
            }
        }

        return FormatRouteName(name);
    }

    /// <summary>
    /// 获取动作名称
    /// </summary>
    private string GetActionName(MethodInfo methodInfo)
    {
        var name = methodInfo.Name;

        // 移除 Async 后缀
        if (name.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^5];
        }

        return FormatRouteName(name);
    }

    /// <summary>
    /// 获取 HTTP 方法
    /// </summary>
    private string GetHttpMethod(MethodInfo methodInfo)
    {
        // 检查是否有 HTTP 方法特性
        var httpMethodAttr = methodInfo.GetCustomAttribute<HttpMethodAttribute>();
        if (httpMethodAttr != null)
        {
            return httpMethodAttr.HttpMethods.First();
        }

        // 根据方法名推断 HTTP 方法
        var methodName = methodInfo.Name;
        foreach (var convention in _options.Conventions.HttpMethodConventions)
        {
            if (methodName.StartsWith(convention.Key, StringComparison.OrdinalIgnoreCase))
            {
                return convention.Value;
            }
        }

        // 默认为 POST
        return "POST";
    }

    /// <summary>
    /// 获取路由模板
    /// </summary>
    private string GetRouteTemplate(DynamicApiConventionContext context)
    {
        // 如果是控制器级别的路由（没有方法信息），返回控制器基础路径
        if (context.MethodInfo == null)
        {
            return GetControllerRouteTemplate(context);
        }

        // 如果是方法级别的路由，返回方法特定的部分
        return GetActionRouteTemplate(context);
    }

    /// <summary>
    /// 获取控制器级别的路由模板
    /// </summary>
    private string GetControllerRouteTemplate(DynamicApiConventionContext context)
    {
        var parts = new List<string>();

        // 添加路由前缀
        if (!string.IsNullOrEmpty(_options.DefaultRoutePrefix))
        {
            parts.Add(_options.DefaultRoutePrefix);
        }

        // 添加 API 版本
        if (_options.EnableApiVersioning && !string.IsNullOrEmpty(context.ApiVersion))
        {
            parts.Add($"v{context.ApiVersion}");
        }

        // 添加模块名称
        if (_options.Routes.UseModuleNameAsRoute)
        {
            var moduleName = GetModuleName(context.ServiceType);
            if (!string.IsNullOrEmpty(moduleName))
            {
                parts.Add(FormatRouteName(moduleName));
            }
        }

        // 添加控制器名称
        if (!string.IsNullOrEmpty(context.ControllerName))
        {
            parts.Add(context.ControllerName);
        }

        return string.Join("/", parts);
    }

    /// <summary>
    /// 获取动作方法级别的路由模板
    /// </summary>
    private string GetActionRouteTemplate(DynamicApiConventionContext context)
    {
        var parts = new List<string>();

        // 总是添加动作名称（包括标准的 CRUD 操作）
        if (!string.IsNullOrEmpty(context.ActionName))
        {
            parts.Add(context.ActionName);
        }

        // 添加路由参数
        if (context.MethodInfo != null)
        {
            var parameters = GetRouteParameters(context.MethodInfo, context.HttpMethod);
            if (parameters.Count != 0)
            {
                parts.AddRange(parameters);
            }
        }

        return string.Join("/", parts);
    }

    /// <summary>
    /// 获取模块名称
    /// </summary>
    private string? GetModuleName(Type serviceType)
    {
        if (!_options.Routes.UseModuleNameAsRoute)
        {
            return null;
        }

        var namespaceName = serviceType.Namespace;
        if (string.IsNullOrEmpty(namespaceName))
        {
            return null;
        }

        // 移除排除的命名空间前缀
        foreach (var prefix in _options.Routes.NamespacePrefixesToExclude)
        {
            if (namespaceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                namespaceName = namespaceName[prefix.Length..].TrimStart('.');
                break;
            }
        }

        // 提取模块名称
        if (!string.IsNullOrEmpty(_options.Routes.ModuleNamePattern))
        {
            var match = Regex.Match(namespaceName, _options.Routes.ModuleNamePattern);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
        }

        // 默认使用第一个命名空间段
        var segments = namespaceName.Split('.');
        return segments.Length > 0 ? segments[0] : null;
    }

    /// <summary>
    /// 格式化路由名称
    /// </summary>
    private string FormatRouteName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (_options.Conventions.UsePascalCaseRoutes)
        {
            return name;
        }

        // 将 PascalCase 转换为 kebab-case 或其他格式
        var result = Regex.Replace(name, "([a-z])([A-Z])", $"$1{_options.Conventions.RouteSeparator}$2");

        return _options.Conventions.UseLowercaseRoutes ? result.ToLower() : result;
    }
}

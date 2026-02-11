#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultDynamicApiConvention
// Guid:d5967adb-2c33-4a0c-93e0-ae7e768edbed
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using System.Text.RegularExpressions;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;
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

        // 设置 API 版本
        if (string.IsNullOrEmpty(context.ApiVersion))
        {
            if (context.MethodInfo == null)
            {
                // 控制器级别：从类级别或配置获取
                context.ApiVersion = GetApiVersion(context.ServiceType, null) ?? _options.DefaultApiVersion;
            }
            else
            {
                // 方法级别：优先从方法获取，然后从类级别，最后从配置
                context.ApiVersion = GetApiVersion(context.ServiceType, context.MethodInfo) ?? _options.DefaultApiVersion;
            }
        }

        // 如果没有方法信息，设置控制器级别的路由模板后返回
        if (context.MethodInfo == null)
        {
            // 从类级别的 DynamicApiAttribute 读取自定义路由模板
            var classAttr = context.ServiceType.GetCustomAttribute<DynamicApiAttribute>();
            if (classAttr != null && !string.IsNullOrEmpty(classAttr.RouteTemplate))
            {
                context.RouteTemplate = classAttr.RouteTemplate;
            }
            // 如果没有自定义路由模板，使用默认规则
            else if (string.IsNullOrEmpty(context.RouteTemplate))
            {
                context.RouteTemplate = GetRouteTemplate(context);
            }
            return;
        }

        // 从方法级别的 DynamicApiAttribute 读取自定义名称
        var methodAttr = context.MethodInfo.GetCustomAttribute<DynamicApiAttribute>();
        if (methodAttr != null && !string.IsNullOrEmpty(methodAttr.Name))
        {
            context.ActionName = methodAttr.Name;
        }
        // 设置动作名称
        else if (string.IsNullOrEmpty(context.ActionName))
        {
            context.ActionName = GetActionName(context.MethodInfo);
        }

        // 设置 HTTP 方法
        if (string.IsNullOrEmpty(context.HttpMethod))
        {
            context.HttpMethod = GetHttpMethod(context.MethodInfo);
        }

        // 从方法级别的 DynamicApiAttribute 读取自定义路由模板
        if (methodAttr != null && !string.IsNullOrEmpty(methodAttr.RouteTemplate))
        {
            context.RouteTemplate = methodAttr.RouteTemplate;
        }
        // 设置方法级别的路由模板
        else if (string.IsNullOrEmpty(context.RouteTemplate))
        {
            context.RouteTemplate = GetRouteTemplate(context);
        }
    }

    /// <summary>
    /// 检查是否禁用动态 API
    /// </summary>
    private static bool IsDisabled(Type serviceType, MethodInfo? methodInfo)
    {
        // 检查类级别的动态 API 特性（可能有多个）
        var classAttributes = serviceType.GetCustomAttributes<DynamicApiAttribute>();
        foreach (var attr in classAttributes)
        {
            if (!attr.IsEnabled)
            {
                return true;
            }
        }

        // 检查方法级别的禁用标记（可能有多个）
        if (methodInfo != null)
        {
            var methodAttributes = methodInfo.GetCustomAttributes<DynamicApiAttribute>();
            foreach (var attr in methodAttributes)
            {
                if (!attr.IsEnabled)
                {
                    return true;
                }
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
    /// 获取 API 版本（获取单个版本，用于约定系统）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息（可选）</param>
    /// <returns>API版本号，优先级：MapToApiVersion > DynamicApi.Version(方法) > ApiVersion(方法) > DynamicApi.Version(类) > ApiVersion(类)</returns>
    private static string? GetApiVersion(Type serviceType, MethodInfo? methodInfo = null)
    {
        // 优先从方法级别获取版本号
        if (methodInfo != null)
        {
            // 1. 最高优先级：MapToApiVersionAttribute（取第一个）
            var mapToVersionAttr = methodInfo.GetCustomAttributes<MapToApiVersionAttribute>().FirstOrDefault();
            if (mapToVersionAttr != null && !string.IsNullOrEmpty(mapToVersionAttr.Version))
            {
                return mapToVersionAttr.Version;
            }

            // 2. 次优先级：DynamicApiAttribute.Version（取第一个有版本号的）
            var dynamicApiAttrs = methodInfo.GetCustomAttributes<DynamicApiAttribute>();
            foreach (var attr in dynamicApiAttrs)
            {
                if (!string.IsNullOrEmpty(attr.Version))
                {
                    return attr.Version;
                }
            }

            // 3. 标准 ApiVersionAttribute（取第一个）
            var apiVersionAttr = methodInfo.GetCustomAttributes<ApiVersionAttribute>().FirstOrDefault();
            if (apiVersionAttr != null && !string.IsNullOrEmpty(apiVersionAttr.Version))
            {
                return apiVersionAttr.Version;
            }
        }

        // 然后从类级别获取版本号
        // 4. 类级别的 DynamicApiAttribute.Version（取第一个有版本号的）
        var classDynamicApiAttrs = serviceType.GetCustomAttributes<DynamicApiAttribute>();
        foreach (var attr in classDynamicApiAttrs)
        {
            if (!string.IsNullOrEmpty(attr.Version))
            {
                return attr.Version;
            }
        }

        // 5. 类级别的 ApiVersionAttribute（取第一个）
        var classApiVersionAttr = serviceType.GetCustomAttributes<ApiVersionAttribute>().FirstOrDefault();
        if (classApiVersionAttr != null && !string.IsNullOrEmpty(classApiVersionAttr.Version))
        {
            return classApiVersionAttr.Version;
        }

        return null;
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
            var paramName = parameter.Name ?? string.Empty;

            // 只将 ID 相关的简单类型参数添加到路由，参数名以 "Id"/"ID" 结尾
            var isIdParameter = paramName.EndsWith("Id", StringComparison.Ordinal) ||
                               paramName.EndsWith("ID", StringComparison.Ordinal);

            if (!isIdParameter || !IsSimpleType(parameter.ParameterType))
            {
                continue;
            }

            // 对于 GET 和 DELETE 请求，ID 参数添加到路由
            if (httpMethod is "GET" or "DELETE")
            {
                parameters.Add($"{{{parameter.Name}}}");
            }
            // 对于 PUT 和 PATCH 请求，通常第一个 ID 参数添加到路由
            else if (httpMethod is "PUT" or "PATCH")
            {
                if (parameters.Count == 0)
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

        // 如果启用了 API 版本控制，且方法有版本号
        if (_options.EnableApiVersioning && context.MethodInfo != null && !string.IsNullOrEmpty(context.ApiVersion))
        {
            // 获取类级别的版本号
            var classVersion = GetApiVersion(context.ServiceType, null) ?? _options.DefaultApiVersion;

            // 如果方法级别的版本号与类级别不同（或类级别没有版本号），在方法路由中添加版本号
            if (string.IsNullOrEmpty(classVersion) || context.ApiVersion != classVersion)
            {
                parts.Add($"v{context.ApiVersion}");
            }
        }

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

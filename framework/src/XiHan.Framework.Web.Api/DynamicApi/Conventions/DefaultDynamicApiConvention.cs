// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using System.Text.RegularExpressions;
using XiHan.Framework.Web.Api.DynamicApi.Helpers;
using XiHan.Framework.Web.Api.DynamicApi.Options;
using XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

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

        var usePascalCaseRoute = ResolveUsePascalCaseRoute(context.ServiceType, context.MethodInfo);
        var useLowercaseRoute = ResolveUseLowercaseRoute(context.ServiceType, context.MethodInfo);
        var preserveRoutePredicate = ResolvePreserveRoutePredicate(context.ServiceType, context.MethodInfo);
        var customProperties = ResolveCustomProperties(context.ServiceType, context.MethodInfo);
        if (customProperties.Count != 0)
        {
            context.Metadata["DynamicApi.CustomProperties"] = customProperties;
        }

        // 设置控制器名称
        if (string.IsNullOrEmpty(context.ControllerName))
        {
            context.ControllerName = GetControllerName(context.ServiceType, usePascalCaseRoute, useLowercaseRoute);
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
            // 从类级别合并结果读取自定义路由模板
            var classRouteTemplate = DynamicApiAttributeMergeHelper.ResolveRouteTemplate(context.ServiceType);
            if (!string.IsNullOrWhiteSpace(classRouteTemplate))
            {
                context.RouteTemplate = classRouteTemplate;
            }
            // 如果没有自定义路由模板，使用默认规则
            else if (string.IsNullOrEmpty(context.RouteTemplate))
            {
                context.RouteTemplate = GetRouteTemplate(context, usePascalCaseRoute, useLowercaseRoute);
            }
            return;
        }

        // 从方法级别合并结果读取自定义名称
        var methodAttributes = DynamicApiAttributeMergeHelper.GetOrderedMethodAttributes(context.MethodInfo);
        var methodCustomName = DynamicApiAttributeMergeHelper.ResolveStringFromAttributes(
            methodAttributes,
            attribute => attribute.Name);
        if (!string.IsNullOrWhiteSpace(methodCustomName))
        {
            context.ActionName = FormatRouteName(methodCustomName, usePascalCaseRoute, useLowercaseRoute);
        }
        // 设置动作名称
        else if (string.IsNullOrEmpty(context.ActionName))
        {
            context.ActionName = GetActionName(context.MethodInfo, preserveRoutePredicate, usePascalCaseRoute, useLowercaseRoute);
        }

        // 设置 HTTP 方法
        if (string.IsNullOrEmpty(context.HttpMethod))
        {
            context.HttpMethod = GetHttpMethod(context.MethodInfo);
        }

        // 从方法级别的 DynamicApiAttribute 读取自定义路由模板
        var methodRouteTemplate = DynamicApiAttributeMergeHelper.ResolveStringFromAttributes(
            methodAttributes,
            attribute => attribute.RouteTemplate);
        if (!string.IsNullOrWhiteSpace(methodRouteTemplate))
        {
            context.RouteTemplate = methodRouteTemplate;
        }
        // 设置方法级别的路由模板
        else if (string.IsNullOrEmpty(context.RouteTemplate))
        {
            context.RouteTemplate = GetRouteTemplate(context, usePascalCaseRoute, useLowercaseRoute);
        }
    }

    /// <summary>
    /// 检查是否禁用动态 API
    /// </summary>
    private static bool IsDisabled(Type serviceType, MethodInfo? methodInfo)
    {
        return !DynamicApiAttributeMergeHelper.IsEnabled(serviceType, methodInfo);
    }

    /// <summary>
    /// 获取 API 版本（获取单个版本，用于约定系统）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息（可选）</param>
    /// <returns>API版本号，优先级：DynamicApi.Version(方法) > DynamicApi.Version(类)</returns>
    private static string? GetApiVersion(Type serviceType, MethodInfo? methodInfo = null)
    {
        return DynamicApiAttributeMergeHelper.ResolveVersion(serviceType, methodInfo);
    }

    /// <summary>
    /// 获取路由参数
    /// </summary>
    /// <remarks>
    /// 只有显式标注 <see cref="Microsoft.AspNetCore.Mvc.FromRouteAttribute"/> 的参数才成为路由段。
    /// 不按参数名后缀推断：名称是实现细节，让它决定 URL 会使「给既有方法加一个参数」
    /// 变成静默的线上破坏性变更。
    /// </remarks>
    private static List<string> GetRouteParameters(MethodInfo methodInfo)
    {
        var parameters = new List<string>();

        foreach (var parameter in methodInfo.GetParameters())
        {
            var paramName = parameter.Name;
            if (string.IsNullOrWhiteSpace(paramName))
            {
                continue;
            }

            if (!ParameterSourceResolver.TryGetExplicitBinding(parameter, out var explicitSource, out var bindingName) ||
                explicitSource != ParameterSource.Route)
            {
                continue;
            }

            parameters.Add($"{{{bindingName ?? paramName}}}");
        }

        return parameters;
    }

    /// <summary>
    /// 解析并合并自定义属性（方法级覆盖类级）
    /// </summary>
    private static Dictionary<string, string> ResolveCustomProperties(Type serviceType, MethodInfo? methodInfo)
    {
        return new Dictionary<string, string>(
            DynamicApiAttributeMergeHelper.ResolveCustomProperties(serviceType, methodInfo),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取控制器名称
    /// </summary>
    private string GetControllerName(Type serviceType, bool usePascalCaseRoute, bool useLowercaseRoute)
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

        return FormatRouteName(name, usePascalCaseRoute, useLowercaseRoute);
    }

    /// <summary>
    /// 获取动作名称
    /// </summary>
    private string GetActionName(
        MethodInfo methodInfo,
        bool preserveRoutePredicate,
        bool usePascalCaseRoute,
        bool useLowercaseRoute)
    {
        var name = methodInfo.Name;

        // 移除 Async 后缀
        if (name.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^5];
        }

        if (!preserveRoutePredicate)
        {
            name = RemoveRoutePredicate(name);
        }

        return FormatRouteName(name, usePascalCaseRoute, useLowercaseRoute);
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
        foreach (var prefix in OrderedRoutePredicates())
        {
            if (IsRoutePredicateMatch(methodName, prefix))
            {
                return _options.Conventions.HttpMethodConventions[prefix];
            }
        }

        // 默认为 POST
        return "POST";
    }

    /// <summary>
    /// 按长度降序枚举动词前缀
    /// </summary>
    /// <remarks>
    /// HTTP 方法推断与前缀剥离共用同一顺序，避免两者对同一方法名得出不一致的结论。
    /// 长度降序保证 PartialUpdate 先于 Patch 被匹配。
    /// </remarks>
    /// <returns></returns>
    private IEnumerable<string> OrderedRoutePredicates()
    {
        return _options.Conventions.HttpMethodConventions.Keys.OrderByDescending(prefix => prefix.Length);
    }

    /// <summary>
    /// 判断方法名是否以指定动词前缀开头，且前缀之后是一个新词
    /// </summary>
    /// <remarks>
    /// 要求词边界：前缀之后须为大写字母或下划线，否则 AddressBook 会被 Add 命中而得到
    /// POST /ressBook、EditorTemplate 会被 Edit 命中而得到 PUT /orTemplate。
    /// 方法名与前缀完全相等时视为命中。
    /// </remarks>
    /// <param name="name">方法名</param>
    /// <param name="prefix">动词前缀</param>
    /// <returns></returns>
    private static bool IsRoutePredicateMatch(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Length == prefix.Length)
        {
            return true;
        }

        var next = name[prefix.Length];

        return char.IsUpper(next) || next == '_';
    }

    /// <summary>
    /// 获取路由模板
    /// </summary>
    private string GetRouteTemplate(DynamicApiConventionContext context, bool usePascalCaseRoute, bool useLowercaseRoute)
    {
        // 如果是控制器级别的路由（没有方法信息），返回控制器基础路径
        if (context.MethodInfo == null)
        {
            return GetControllerRouteTemplate(context, usePascalCaseRoute, useLowercaseRoute);
        }

        // 如果是方法级别的路由，返回方法特定的部分
        return GetActionRouteTemplate(context);
    }

    /// <summary>
    /// 获取控制器级别的路由模板
    /// </summary>
    private string GetControllerRouteTemplate(
        DynamicApiConventionContext context,
        bool usePascalCaseRoute,
        bool useLowercaseRoute)
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

        // 添加命名空间路由段（启用后优先于模块路由，避免重复）
        if (_options.Routes.UseNamespaceAsRoute)
        {
            var namespaceSegments = GetNamespaceRouteSegments(context.ServiceType, usePascalCaseRoute, useLowercaseRoute);
            if (namespaceSegments.Count != 0)
            {
                parts.AddRange(namespaceSegments);
            }
        }
        // 添加模块名称
        else if (_options.Routes.UseModuleNameAsRoute)
        {
            var moduleName = GetModuleName(context.ServiceType);
            if (!string.IsNullOrEmpty(moduleName))
            {
                parts.Add(FormatRouteName(moduleName, usePascalCaseRoute, useLowercaseRoute));
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
            var parameters = GetRouteParameters(context.MethodInfo);
            if (parameters.Count != 0)
            {
                parts.AddRange(parameters);
            }
        }

        return string.Join("/", parts);
    }

    /// <summary>
    /// 获取命名空间路由段
    /// </summary>
    private List<string> GetNamespaceRouteSegments(Type serviceType, bool usePascalCaseRoute, bool useLowercaseRoute)
    {
        if (!_options.Routes.UseNamespaceAsRoute)
        {
            return [];
        }

        var namespaceName = GetNormalizedNamespaceName(serviceType);
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return [];
        }

        return namespaceName
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => FormatRouteName(segment, usePascalCaseRoute, useLowercaseRoute))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();
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

        var namespaceName = GetNormalizedNamespaceName(serviceType);
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return null;
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
    /// 获取规范化后的命名空间（移除配置中的前缀）
    /// </summary>
    private string? GetNormalizedNamespaceName(Type serviceType)
    {
        var namespaceName = serviceType.Namespace;
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return null;
        }

        var prefixes = _options.Routes.NamespacePrefixesToExclude
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .OrderByDescending(prefix => prefix.Length);

        foreach (var prefix in prefixes)
        {
            if (namespaceName.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var matchPrefix = $"{prefix}.";
            if (namespaceName.StartsWith(matchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return namespaceName[matchPrefix.Length..].TrimStart('.');
            }
        }

        return namespaceName.Trim('.');
    }

    /// <summary>
    /// 格式化路由名称
    /// </summary>
    private string FormatRouteName(string name, bool usePascalCaseRoute, bool useLowercaseRoute)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (usePascalCaseRoute)
        {
            return useLowercaseRoute ? name.ToLowerInvariant() : name;
        }

        // 将 PascalCase 转换为 kebab-case 或其他格式
        var result = Regex.Replace(name, "([a-z])([A-Z])", $"$1{_options.Conventions.RouteSeparator}$2");

        return useLowercaseRoute ? result.ToLowerInvariant() : result;
    }

    /// <summary>
    /// 解析是否使用 PascalCase 路由
    /// </summary>
    private bool ResolveUsePascalCaseRoute(Type serviceType, MethodInfo? methodInfo)
    {
        return DynamicApiAttributeMergeHelper.ResolveBoolFromMethodThenClass(
            serviceType,
            methodInfo,
            attribute => attribute.UsePascalCaseRoutesOrNull,
            _options.Conventions.UsePascalCaseRoutes);
    }

    /// <summary>
    /// 解析是否使用小写路由
    /// </summary>
    private bool ResolveUseLowercaseRoute(Type serviceType, MethodInfo? methodInfo)
    {
        return DynamicApiAttributeMergeHelper.ResolveBoolFromMethodThenClass(
            serviceType,
            methodInfo,
            attribute => attribute.UseLowercaseRouteOrNull,
            _options.Conventions.UseLowercaseRoutes);
    }

    /// <summary>
    /// 解析是否保留路由谓词
    /// </summary>
    private bool ResolvePreserveRoutePredicate(Type serviceType, MethodInfo? methodInfo)
    {
        return DynamicApiAttributeMergeHelper.ResolveBoolFromMethodThenClass(
            serviceType,
            methodInfo,
            attribute => attribute.PreserveRoutePredicateOrNull,
            _options.Conventions.PreserveRoutePredicate);
    }

    /// <summary>
    /// 移除路由谓词（方法名前缀）
    /// </summary>
    private string RemoveRoutePredicate(string name)
    {
        foreach (var prefix in OrderedRoutePredicates())
        {
            if (!IsRoutePredicateMatch(name, prefix))
            {
                continue;
            }

            var candidate = name[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return name;
    }
}

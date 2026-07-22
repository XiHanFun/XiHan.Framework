// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数来源解析器
/// </summary>
public class ParameterSourceResolver
{
    private readonly string _httpMethod;
    private int _bodyParameterCount;
    private int _routeIdParameterCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpMethod">HTTP 方法</param>
    public ParameterSourceResolver(string httpMethod)
    {
        _httpMethod = httpMethod.ToUpperInvariant();
        _bodyParameterCount = 0;
        _routeIdParameterCount = 0;
    }

    /// <summary>
    /// 获取当前 Body 参数数量
    /// </summary>
    public int BodyParameterCount => _bodyParameterCount;

    /// <summary>
    /// 解析参数来源
    /// </summary>
    public ParameterSource Resolve(ParameterDescriptor descriptor)
    {
        if (descriptor.ParameterInfo == null)
        {
            throw new ArgumentNullException(nameof(descriptor.ParameterInfo));
        }

        // 1.显式特性优先（最高优先级）
        if (TryGetExplicitBinding(descriptor.ParameterInfo, out var explicitSource, out _))
        {
            descriptor.IsExplicit = true;

            // 如果显式标注为 Body，增加计数
            if (explicitSource == ParameterSource.Body)
            {
                _bodyParameterCount++;
            }
            // 如果显式标注为 Route，计入路由参数计数，避免 PUT/PATCH 继续追加自动 Id 路由
            else if (explicitSource == ParameterSource.Route)
            {
                _routeIdParameterCount++;
            }

            return explicitSource;
        }

        // 2.基础设施参数直接跳过
        if (descriptor.Kind == ParameterKind.Special)
        {
            return ParameterSource.Services;
        }

        // 3.根据 HTTP Method 决策
        var allowBody = IsBodyAllowed();

        // 4.表单文件参数推断
        if (allowBody && ParameterClassifier.ContainsFormFile(descriptor.Type))
        {
            return ParameterSource.Form;
        }

        // 5.Route 参数推断
        if (ShouldBindIdFromRoute(descriptor))
        {
            _routeIdParameterCount++;
            return ParameterSource.Route;
        }

        // 6.Body 参数推断（只能 1 个）
        if (allowBody && descriptor.Kind == ParameterKind.Complex)
        {
            // 确保只有一个 Body 参数
            if (_bodyParameterCount == 0)
            {
                _bodyParameterCount++;
                return ParameterSource.Body;
            }

            // 如果已经有 Body 参数，复杂类型降级为 Query
            return ParameterSource.Query;
        }

        // 7.Query 参数兜底规则
        return ParameterSource.Query;
    }

    /// <summary>
    /// 尝试获取显式绑定配置
    /// </summary>
    /// <param name="parameter">参数信息</param>
    /// <param name="source">绑定来源</param>
    /// <param name="bindingName">绑定名称（Route/Query/Header/Form 可能有）</param>
    /// <returns>是否存在显式绑定</returns>
    public static bool TryGetExplicitBinding(ParameterInfo parameter, out ParameterSource source, out string? bindingName)
    {
        if (parameter.GetCustomAttribute<FromRouteAttribute>() is { } fromRoute)
        {
            source = ParameterSource.Route;
            bindingName = NormalizeBindingName(fromRoute.Name);
            return true;
        }

        if (parameter.GetCustomAttribute<FromQueryAttribute>() is { } fromQuery)
        {
            source = ParameterSource.Query;
            bindingName = NormalizeBindingName(fromQuery.Name);
            return true;
        }

        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
        {
            source = ParameterSource.Body;
            bindingName = null;
            return true;
        }

        if (parameter.GetCustomAttribute<FromServicesAttribute>() != null)
        {
            source = ParameterSource.Services;
            bindingName = null;
            return true;
        }

        if (parameter.GetCustomAttribute<FromHeaderAttribute>() is { } fromHeader)
        {
            source = ParameterSource.Header;
            bindingName = NormalizeBindingName(fromHeader.Name);
            return true;
        }

        if (parameter.GetCustomAttribute<FromFormAttribute>() is { } fromForm)
        {
            source = ParameterSource.Form;
            bindingName = NormalizeBindingName(fromForm.Name);
            return true;
        }

        source = default;
        bindingName = null;
        return false;
    }

    /// <summary>
    /// 规范化绑定名称
    /// </summary>
    private static string? NormalizeBindingName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    /// <summary>
    /// 判断是否允许 Body 参数
    /// </summary>
    private bool IsBodyAllowed()
    {
        // GET / DELETE / HEAD 不允许 Body
        return _httpMethod is not "GET" and not "DELETE" and not "HEAD";
    }

    /// <summary>
    /// 判断是否应将 Id 参数绑定到 Route
    /// </summary>
    private bool ShouldBindIdFromRoute(ParameterDescriptor descriptor)
    {
        if (descriptor.Role != ParameterRole.Id || descriptor.Kind != ParameterKind.Simple)
        {
            return false;
        }

        return _httpMethod switch
        {
            "GET" or "DELETE" or "HEAD" => true,
            "PUT" or "PATCH" => _routeIdParameterCount == 0,
            _ => false
        };
    }
}

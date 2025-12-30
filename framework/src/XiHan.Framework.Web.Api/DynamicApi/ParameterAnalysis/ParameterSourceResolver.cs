#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParameterSourceResolver
// Guid:param-source-resolver-dynamic-api-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpMethod">HTTP 方法</param>
    public ParameterSourceResolver(string httpMethod)
    {
        _httpMethod = httpMethod.ToUpperInvariant();
        _bodyParameterCount = 0;
    }

    /// <summary>
    /// 解析参数来源
    /// </summary>
    public ParameterSource Resolve(ParameterDescriptor descriptor)
    {
        if (descriptor.ParameterInfo == null)
        {
            throw new ArgumentNullException(nameof(descriptor.ParameterInfo));
        }

        // Step 0：显式特性优先（最高优先级）
        var explicitSource = GetExplicitSource(descriptor.ParameterInfo);
        if (explicitSource.HasValue)
        {
            descriptor.IsExplicit = true;

            // 如果显式标注为 Body，增加计数
            if (explicitSource.Value == ParameterSource.Body)
            {
                _bodyParameterCount++;
            }

            return explicitSource.Value;
        }

        // Step 1：基础设施参数直接跳过
        if (descriptor.Kind == ParameterKind.Special)
        {
            return ParameterSource.Services;
        }

        // Step 2：根据 HTTP Method 决策
        var allowBody = IsBodyAllowed();

        // Step 3：Route 参数推断
        if (descriptor.Role == ParameterRole.Id && descriptor.Kind == ParameterKind.Simple)
        {
            return ParameterSource.Route;
        }

        // Step 4：Body 参数推断（只能 1 个）
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

        // Step 5：Query 参数兜底规则
        return ParameterSource.Query;
    }

    /// <summary>
    /// 获取显式标注的参数来源
    /// </summary>
    private static ParameterSource? GetExplicitSource(ParameterInfo parameter)
    {
        if (parameter.GetCustomAttribute<FromRouteAttribute>() != null)
        {
            return ParameterSource.Route;
        }

        if (parameter.GetCustomAttribute<FromQueryAttribute>() != null)
        {
            return ParameterSource.Query;
        }

        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
        {
            return ParameterSource.Body;
        }

        if (parameter.GetCustomAttribute<FromServicesAttribute>() != null)
        {
            return ParameterSource.Services;
        }

        if (parameter.GetCustomAttribute<FromHeaderAttribute>() != null)
        {
            return ParameterSource.Header;
        }

        if (parameter.GetCustomAttribute<FromFormAttribute>() != null)
        {
            return ParameterSource.Form;
        }

        return null;
    }

    /// <summary>
    /// 判断是否允许 Body 参数
    /// </summary>
    private bool IsBodyAllowed()
    {
        // GET / DELETE 不允许 Body
        return _httpMethod != "GET" && _httpMethod != "DELETE";
    }

    /// <summary>
    /// 获取当前 Body 参数数量
    /// </summary>
    public int BodyParameterCount => _bodyParameterCount;
}


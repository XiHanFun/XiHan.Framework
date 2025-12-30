#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiParameterAnalyzer
// Guid:param-analyzer-dynamic-api-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 动态 API 参数分析器（总入口）
/// </summary>
public class DynamicApiParameterAnalyzer
{
    /// <summary>
    /// 分析方法参数
    /// </summary>
    /// <param name="methodInfo">方法信息</param>
    /// <param name="httpMethod">HTTP 方法</param>
    /// <returns>参数描述符列表</returns>
    public static List<ParameterDescriptor> Analyze(MethodInfo methodInfo, string httpMethod)
    {
        var methodName = methodInfo.Name;
        var parameters = methodInfo.GetParameters();

        // 创建解析器
        var sourceResolver = new ParameterSourceResolver(httpMethod);
        var validator = new ParameterRuleValidator(httpMethod, methodName);

        var descriptors = new List<ParameterDescriptor>();

        // 第一遍：分类和初步分析
        foreach (var parameter in parameters)
        {
            var descriptor = AnalyzeParameter(parameter, methodName);
            descriptors.Add(descriptor);
        }

        // 第二遍：解析参数来源
        foreach (var descriptor in descriptors)
        {
            descriptor.Source = sourceResolver.Resolve(descriptor);
        }

        // 第三遍：确定 Required 属性
        foreach (var descriptor in descriptors)
        {
            descriptor.Required = DetermineRequired(descriptor);
        }

        // 校验规则
        try
        {
            validator.Validate(descriptors);
        }
        catch (DynamicApiException ex)
        {
            // 添加更多上下文信息
            throw new DynamicApiException(
                $"参数分析失败 - {methodInfo.DeclaringType?.Name}.{methodName}: {ex.Message}",
                ex);
        }

        return descriptors;
    }

    /// <summary>
    /// 分析单个参数
    /// </summary>
    private static ParameterDescriptor AnalyzeParameter(ParameterInfo parameter, string methodName)
    {
        var type = parameter.ParameterType;
        var name = parameter.Name ?? "unknown";

        // 分类参数种类
        var kind = ParameterClassifier.ClassifyKind(type);

        // 分类参数角色
        var role = ParameterClassifier.ClassifyRole(parameter, methodName);

        // 创建描述符
        var descriptor = new ParameterDescriptor
        {
            Name = name,
            Type = type,
            Kind = kind,
            Role = role,
            ParameterInfo = parameter,
            HasDefaultValue = parameter.HasDefaultValue,
            DefaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : null
        };

        return descriptor;
    }

    /// <summary>
    /// 确定参数是否必需
    /// </summary>
    private static bool DetermineRequired(ParameterDescriptor descriptor)
    {
        // 有默认值的参数不是必需的
        if (descriptor.HasDefaultValue)
        {
            return false;
        }

        // 可空类型不是必需的
        if (IsNullable(descriptor.Type))
        {
            return false;
        }

        // Route 参数通常是必需的
        if (descriptor.Source == ParameterSource.Route)
        {
            return true;
        }

        // Body 参数通常是必需的
        if (descriptor.Source == ParameterSource.Body)
        {
            return true;
        }

        // 基础设施参数由框架注入，不需要客户端提供
        if (descriptor.Source == ParameterSource.Services)
        {
            return false;
        }

        // Query 参数默认可选
        return false;
    }

    /// <summary>
    /// 判断类型是否可空
    /// </summary>
    private static bool IsNullable(Type type)
    {
        // 引用类型（除了 string 在 nullable reference types 启用时）
        if (!type.IsValueType)
        {
            return true;
        }

        // Nullable<T>
        return Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// 获取 Route 参数名称列表
    /// </summary>
    public static List<string> GetRouteParameterNames(List<ParameterDescriptor> descriptors)
    {
        return descriptors
            .Where(d => d.Source == ParameterSource.Route)
            .Select(d => d.Name)
            .ToList();
    }

    /// <summary>
    /// 获取 Body 参数
    /// </summary>
    public static ParameterDescriptor? GetBodyParameter(List<ParameterDescriptor> descriptors)
    {
        return descriptors.FirstOrDefault(d => d.Source == ParameterSource.Body);
    }

    /// <summary>
    /// 判断是否有参数来自指定来源
    /// </summary>
    public static bool HasParameterFromSource(List<ParameterDescriptor> descriptors, ParameterSource source)
    {
        return descriptors.Any(d => d.Source == source);
    }
}


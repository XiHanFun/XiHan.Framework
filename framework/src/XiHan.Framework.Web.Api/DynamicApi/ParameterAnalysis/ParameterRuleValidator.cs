#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParameterRuleValidator
// Guid:param-rule-validator-dynamic-api-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数规则校验器
/// </summary>
public class ParameterRuleValidator
{
    private readonly string _httpMethod;
    private readonly string _methodName;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpMethod">HTTP 方法</param>
    /// <param name="methodName">方法名称</param>
    public ParameterRuleValidator(string httpMethod, string methodName)
    {
        _httpMethod = httpMethod.ToUpperInvariant();
        _methodName = methodName;
    }

    /// <summary>
    /// 校验参数列表
    /// </summary>
    public void Validate(IEnumerable<ParameterDescriptor> descriptors)
    {
        var descriptorList = descriptors.ToList();

        // 1. FromBody 数量校验
        ValidateBodyParameterCount(descriptorList);

        // 2. GET 不允许 Body
        ValidateGetMethodBody(descriptorList);

        // 3. Route 参数过多
        ValidateRouteParameterCount(descriptorList);

        // 4. 基础类型 FromBody 禁止
        ValidateBodyParameterType(descriptorList);

        // 5. 多个复杂类型参数（除了 Body）
        ValidateMultipleComplexParameters(descriptorList);
    }

    /// <summary>
    /// 校验 FromBody 参数数量
    /// </summary>
    private void ValidateBodyParameterCount(List<ParameterDescriptor> descriptors)
    {
        var bodyCount = descriptors.Count(d => d.Source == ParameterSource.Body);

        if (bodyCount > 1)
        {
            throw new DynamicApiException(
                $"方法 '{_methodName}' 只能有一个 FromBody 参数，" +
                $"当前有 {bodyCount} 个。请合并为单个 DTO 对象。");
        }
    }

    /// <summary>
    /// 校验 GET 方法不允许 Body
    /// </summary>
    private void ValidateGetMethodBody(List<ParameterDescriptor> descriptors)
    {
        if (_httpMethod != "GET" && _httpMethod != "DELETE")
        {
            return;
        }

        var bodyParams = descriptors.Where(d => d.Source == ParameterSource.Body).ToList();

        if (bodyParams.Count != 0)
        {
            var paramNames = string.Join(", ", bodyParams.Select(p => p.Name));
            throw new DynamicApiException(
                $"方法 '{_methodName}' 使用 {_httpMethod} 请求，不允许 FromBody 参数。" +
                $"违规参数: {paramNames}。请改用 FromQuery 或 FromRoute。");
        }
    }

    /// <summary>
    /// 校验 Route 参数数量
    /// </summary>
    private void ValidateRouteParameterCount(List<ParameterDescriptor> descriptors)
    {
        var routeCount = descriptors.Count(d => d.Source == ParameterSource.Route);

        if (routeCount > 3)
        {
            var routeParams = descriptors.Where(d => d.Source == ParameterSource.Route).ToList();
            var paramNames = string.Join(", ", routeParams.Select(p => p.Name));

            throw new DynamicApiException(
                $"方法 '{_methodName}' 的 Route 参数过多（{routeCount} 个）。" +
                $"违规参数: {paramNames}。建议使用复合主键对象或改用 FromQuery。");
        }
    }

    /// <summary>
    /// 校验 Body 参数类型
    /// </summary>
    private void ValidateBodyParameterType(List<ParameterDescriptor> descriptors)
    {
        var invalidBodyParams = descriptors
            .Where(d => d.Source == ParameterSource.Body && d.Kind != ParameterKind.Complex)
            .ToList();

        if (invalidBodyParams.Count != 0)
        {
            var paramInfo = invalidBodyParams.First();
            throw new DynamicApiException(
                $"方法 '{_methodName}' 的参数 '{paramInfo.Name}' 类型为 '{paramInfo.Type.Name}'，" +
                "不能使用 FromBody。FromBody 参数必须是复杂类型（DTO / class / record）。");
        }
    }

    /// <summary>
    /// 校验多个复杂类型参数
    /// </summary>
    private void ValidateMultipleComplexParameters(List<ParameterDescriptor> descriptors)
    {
        var complexParams = descriptors
            .Where(d => d.Kind == ParameterKind.Complex && d.Source != ParameterSource.Body)
            .ToList();

        if (complexParams.Count > 1)
        {
            var paramNames = string.Join(", ", complexParams.Select(p => p.Name));
            throw new DynamicApiException(
                $"方法 '{_methodName}' 有多个复杂类型参数（{complexParams.Count} 个）：{paramNames}。" +
                "建议：1) 使用 [FromBody] 标注其中一个；2) 合并为单个 DTO；3) 改用简单类型参数。");
        }
    }
}

/// <summary>
/// 动态 API 异常
/// </summary>
public class DynamicApiException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiException(string message) : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}


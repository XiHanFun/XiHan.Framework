#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParameterDescriptor
// Guid:param-descriptor-dynamic-api-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数描述符（分析结果）
/// </summary>
public class ParameterDescriptor
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数类型
    /// </summary>
    public Type Type { get; set; } = typeof(object);

    /// <summary>
    /// 参数来源
    /// </summary>
    public ParameterSource Source { get; set; }

    /// <summary>
    /// 参数角色
    /// </summary>
    public ParameterRole Role { get; set; }

    /// <summary>
    /// 参数种类
    /// </summary>
    public ParameterKind Kind { get; set; }

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 是否显式标注
    /// </summary>
    public bool IsExplicit { get; set; }

    /// <summary>
    /// 原始参数信息
    /// </summary>
    public ParameterInfo? ParameterInfo { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 是否有默认值
    /// </summary>
    public bool HasDefaultValue { get; set; }
}


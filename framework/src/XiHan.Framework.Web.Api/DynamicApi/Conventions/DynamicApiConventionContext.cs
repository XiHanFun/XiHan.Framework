// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;

namespace XiHan.Framework.Web.Api.DynamicApi.Conventions;

/// <summary>
/// 动态 API 约定上下文
/// </summary>
public class DynamicApiConventionContext
{
    /// <summary>
    /// 服务类型
    /// </summary>
    public Type ServiceType { get; set; } = null!;

    /// <summary>
    /// 方法信息
    /// </summary>
    public MethodInfo? MethodInfo { get; set; }

    /// <summary>
    /// 控制器名称
    /// </summary>
    public string? ControllerName { get; set; }

    /// <summary>
    /// 动作名称
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// 路由模板
    /// </summary>
    public string? RouteTemplate { get; set; }

    /// <summary>
    /// API 版本
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 额外数据
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiConventionContext
// Guid:b07d870f-d0c8-481d-b0e6-91693d6bce59
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

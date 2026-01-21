#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationBuilderExtensions
// Guid:ba5b6c7d-8e9f-4a1b-9c6d-be3f4a5b6c7d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:55:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using XiHan.Framework.Web.RealTime.Hubs;

namespace XiHan.Framework.Web.RealTime.Extensions;

/// <summary>
/// 应用程序构建器扩展
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 映射曦寒 Hub
    /// </summary>
    /// <typeparam name="THub">Hub 类型</typeparam>
    /// <param name="endpoints">端点路由构建器</param>
    /// <param name="pattern">路径模式</param>
    /// <returns></returns>
    public static HubEndpointConventionBuilder MapXiHanHub<THub>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where THub : XiHanHub
    {
        return endpoints.MapHub<THub>(pattern);
    }

    /// <summary>
    /// 映射曦寒 Hub（带配置）
    /// </summary>
    /// <typeparam name="THub">Hub 类型</typeparam>
    /// <param name="endpoints">端点路由构建器</param>
    /// <param name="pattern">路径模式</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns></returns>
    public static HubEndpointConventionBuilder MapXiHanHub<THub>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Action<Microsoft.AspNetCore.Http.Connections.HttpConnectionDispatcherOptions> configureOptions)
        where THub : XiHanHub
    {
        return endpoints.MapHub<THub>(pattern, configureOptions);
    }
}

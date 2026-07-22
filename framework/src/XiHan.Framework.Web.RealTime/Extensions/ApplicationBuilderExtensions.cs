// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

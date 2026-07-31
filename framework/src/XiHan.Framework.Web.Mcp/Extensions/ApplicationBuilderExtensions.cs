// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.Mcp.Filters;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Extensions;

/// <summary>
/// 应用程序构建器扩展
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 映射曦寒 MCP Server 端点（未就绪暴露时不注册任何端点）
    /// </summary>
    /// <remarks>
    /// <c>AllowAnonymous()</c> 绕过框架全局鉴权 FallbackPolicy，改由 <see cref="McpApiKeyEndpointFilter"/>
    /// 以应用管理的 key 守门。
    /// </remarks>
    /// <param name="endpoints">端点路由构建器</param>
    /// <param name="options">MCP 配置</param>
    /// <returns>端点路由构建器</returns>
    public static IEndpointRouteBuilder MapXiHanMcp(this IEndpointRouteBuilder endpoints, XiHanMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsExposable)
        {
            return endpoints;
        }

        _ = endpoints.MapMcp(options.Path)
            .AllowAnonymous()
            .AddEndpointFilter(new McpApiKeyEndpointFilter(options.ApiKey!, options.HeaderName));

        return endpoints;
    }
}

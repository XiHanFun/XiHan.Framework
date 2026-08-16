// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
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
    /// <exception cref="InvalidOperationException">技能投影出的工具名有冲突（见 <c>SkillMcpToolsConfigurator</c>）</exception>
    public static IEndpointRouteBuilder MapXiHanMcp(this IEndpointRouteBuilder endpoints, XiHanMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsExposable)
        {
            return endpoints;
        }

        // 主动把 McpServerOptions 装配出来：工具集的装配（技能投影、清单裁剪）本是懒的，
        // 不提前跑一遍的话，技能撞名这类装配期错误要等第一个 MCP 请求到达才炸成 500。
        // 宁可让宿主起不来，也不要让它带着「注册过的技能凭空不存在」上线。
        _ = endpoints.ServiceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        _ = endpoints.MapMcp(options.Path)
            .AllowAnonymous()
            .AddEndpointFilter(new McpApiKeyEndpointFilter(options.ApiKey!, options.HeaderName));

        return endpoints;
    }
}

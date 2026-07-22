// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using XiHan.Framework.AI.Mcp;

namespace XiHan.Framework.AI.Extensions.DependencyInjection;

/// <summary>
/// XiHan MCP Server 工具注册扩展
/// </summary>
public static class XiHanMcpServiceCollectionExtensions
{
    /// <summary>
    /// 把技能注册表投影为 MCP server tools（须与官方 <c>AddMcpServer()</c> 配合使用）
    /// </summary>
    /// <remarks>
    /// 经 <see cref="IConfigureOptions{McpServerOptions}"/> 把 <c>IAiSkillRegistry.All</c> 的技能并入 MCP 工具集,
    /// 被官方 <c>AddMcpServer()</c> 的选项管道自动采集。HTTP 传输与端点映射由调用方(WebHost)负责。
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanMcpServerTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<McpServerOptions>, SkillMcpToolsConfigurator>());
        return services;
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using XiHan.Framework.AI.Extensions.DependencyInjection;
using XiHan.Framework.Web.Mcp.Filters;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架 Web MCP 服务集合扩展
/// </summary>
public static class XiHanWebMcpServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒 MCP Server 服务（绑定配置；仅在启用且配了密钥时注册 HTTP 传输与技能工具）
    /// </summary>
    /// <remarks>
    /// fail-closed：<see cref="XiHanMcpOptions.IsExposable"/> 为 false 时只绑定选项、不注册任何 MCP 服务，
    /// 端点映射据同一判定跳过（见 <c>MapXiHanMcp</c>）。
    /// <para>
    /// 就绪暴露时再挂上 <see cref="McpToolExposureFilter"/>，按允许/拒绝清单裁剪对外暴露的工具集；
    /// 两个清单都为空时它什么都不做，暴露面与不挂它时相同。
    /// </para>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanWebMcp(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(XiHanMcpOptions.SectionName);
        services.Configure<XiHanMcpOptions>(section);

        var options = section.Get<XiHanMcpOptions>() ?? new XiHanMcpOptions();
        if (options.IsExposable)
        {
            services.AddMcpServer().WithHttpTransport(transport => transport.Stateless = options.Stateless);
            services.AddXiHanMcpServerTools();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<McpServerOptions>, McpToolExposureFilter>());
        }

        return services;
    }
}

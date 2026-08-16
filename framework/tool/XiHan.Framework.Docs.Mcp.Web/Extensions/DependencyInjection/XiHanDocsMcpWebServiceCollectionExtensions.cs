// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Docs.Mcp.Indexing;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Search;
using XiHan.Framework.Docs.Mcp.Sources;
using XiHan.Framework.Docs.Mcp.Tools;
using XiHan.Framework.Docs.Mcp.Web.Options;

namespace XiHan.Framework.Docs.Mcp.Web.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架文档 MCP Server（HTTP 传输）服务集合扩展
/// </summary>
public static class XiHanDocsMcpWebServiceCollectionExtensions
{
    /// <summary>
    /// 添加文档 MCP Server 服务（绑定配置与检索三层；仅在启用且配了密钥时注册 HTTP 传输与三个工具）
    /// </summary>
    /// <remarks>
    /// fail-closed：<see cref="XiHanDocsMcpWebOptions.IsExposable"/> 为 false 时不注册任何 MCP 服务，
    /// 端点映射据同一判定跳过（见 <c>MapXiHanDocsMcp</c>）。
    /// <para>
    /// 索引与检索这几个单例的注册顺序与内容和 stdio 宿主
    /// (<c>framework/tool/XiHan.Framework.Docs.Mcp/Program.cs</c>) 保持一致：同一套索引、同一套工具，
    /// 两个宿主之间只有传输不同。它们无条件注册，与是否暴露端点无关——建索引这件事不涉及对外暴露。
    /// </para>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <param name="repositoryRoot">仓库根的绝对路径</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanDocsMcpWeb(
        this IServiceCollection services,
        IConfiguration configuration,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var section = configuration.GetSection(XiHanDocsMcpWebOptions.SectionName);
        services.Configure<XiHanDocsMcpWebOptions>(section);

        services.AddSingleton(new DocsMcpOptions());
        services.AddSingleton(new DocSourceLocator(repositoryRoot));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DocIndex>();
        services.AddSingleton<SectionScorer>();
        services.AddSingleton<RelevanceGate>();
        services.AddSingleton(provider => SynonymExpander.Load(
            System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "synonyms.json"),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<SynonymExpander>()));
        services.AddSingleton<DocsMcpTools>();

        var options = section.Get<XiHanDocsMcpWebOptions>() ?? new XiHanDocsMcpWebOptions();
        if (options.IsExposable)
        {
            // WithToolsFromAssembly() 在这里没用：它扫的是调用方所在程序集，
            // 而 DocsMcpTools 在被引用的 XiHan.Framework.Docs.Mcp 里，扫不到。显式登记类型。
            services
                .AddMcpServer()
                .WithHttpTransport(transport => transport.Stateless = options.Stateless)
                .WithTools<DocsMcpTools>();
        }

        return services;
    }
}

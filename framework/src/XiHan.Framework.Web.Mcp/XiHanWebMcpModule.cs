// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.AI;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Core.Extensions;
using XiHan.Framework.Web.Mcp.Extensions;
using XiHan.Framework.Web.Mcp.Extensions.DependencyInjection;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp;

/// <summary>
/// 曦寒框架 Web MCP 服务端模块
/// </summary>
/// <remarks>
/// 把 AI 技能注册表投影出的 MCP tools 经 HTTP 传输暴露为 <c>/mcp</c> 端点，鉴权用应用管理的 key。
/// 由 <c>XiHan:AI:Mcp</c> 配置门控：未开启或未配密钥则既不注册服务也不映射端点（fail-closed）。
/// </remarks>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanAIModule)
    )]
public class XiHanWebMcpModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanWebMcp(services.GetConfiguration());
    }

    /// <summary>
    /// 应用初始化：映射 MCP 端点
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        if (app is not IEndpointRouteBuilder endpoints)
        {
            return;
        }

        var options = app.ApplicationServices.GetRequiredService<IOptions<XiHanMcpOptions>>().Value;
        _ = endpoints.MapXiHanMcp(options);
    }
}

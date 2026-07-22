// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Http;

namespace XiHan.Framework.AI;

/// <summary>
/// 曦寒框架 AI 扩展模块
/// </summary>
[DependsOn(
    typeof(XiHanHttpModule)
    )]
public class XiHanAIModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanAI();
        context.Services.AddXiHanRAG();
    }
}

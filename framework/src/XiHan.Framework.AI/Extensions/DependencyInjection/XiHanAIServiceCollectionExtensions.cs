#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIServiceCollectionExtensions
// Guid:d2467824-af41-4117-ac01-3e6de0d1a98f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Agents;
using XiHan.Framework.AI.Memory;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Prompts;
using XiHan.Framework.AI.Skills;

namespace XiHan.Framework.AI.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class XiHanAIServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒AI服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanAI(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册配置
        services.Configure<AIOptions>(configuration.GetSection(AIOptions.SectionName));
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<KernelMemoryOptions>(configuration.GetSection(KernelMemoryOptions.SectionName));

        // 注册各种服务
        services.AddSingleton<IXiHanAIAgentService, XiHanAIAgentManager>();
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddSingleton<IXiHanAIMemoryService, XiHanAIMemoryService>();
        services.AddSingleton<IXiHanAIPromptManager, XiHanPromptManager>();

        return services;
    }
}

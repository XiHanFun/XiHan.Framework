#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionExtensions
// Guid:d2467824-af41-4117-ac01-3e6de0d1a98f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Agents;
using XiHan.Framework.AI.Memory;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Prompts;
using XiHan.Framework.AI.Skills;

namespace XiHan.Framework.AI.Extensions;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
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
        services.Configure<AiOptions>(configuration.GetSection("XiHan:AI"));
        services.Configure<OpenAiOptions>(configuration.GetSection("XiHan:AI:OpenAI"));
        services.Configure<OllamaOptions>(configuration.GetSection("XiHan:AI:Ollama"));
        services.Configure<KernelMemoryOptions>(configuration.GetSection("XiHan:AI:Memory"));

        // 注册各种服务
        services.AddSingleton<IXiHanAIAgentService, XiHanAIAgentManager>();
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddSingleton<IXiHanAIMemoryService, XiHanAIMemoryService>();
        services.AddSingleton<IXiHanAIPromptManager, XiHanPromptManager>();

        return services;
    }
}

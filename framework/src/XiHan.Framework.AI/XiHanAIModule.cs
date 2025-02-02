﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIModule
// Guid:60bb5691-f03a-4596-84cb-8600003b8fd9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:27:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Providers.Ollama;
using XiHan.Framework.AI.Providers.OpenAI;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.AI;

/// <summary>
/// 曦寒框架人工智能模块
/// </summary>
public class XiHanAIModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        Configure<XiHanAIOptions>(configuration.GetSection("XiHan:AI"));

        // 注册 Semantic Kernel 内核，配置 OpenAI、Ollama 等 Connector
        _ = services.AddSingleton<Kernel>(provider =>
        {
            var builder = Kernel.CreateBuilder();

            // Ollama
            var ollamaOptions = services.GetRequiredService<IOptions<XiHanOllamaOptions>>().Value;
            if (ollamaOptions is not null)
            {
#pragma warning disable SKEXP0070
                _ = builder.Services.AddOllamaChatCompletion(ollamaOptions.ModelId, new Uri(ollamaOptions.Endpoint));
#pragma warning restore SKEXP0070
            }
            // OpenAI
            var openAIOptions = services.GetRequiredService<IOptions<XiHanOpenAIOptions>>().Value;
            if (openAIOptions is not null)
            {
                _ = builder.Services.AddOpenAIChatCompletion(openAIOptions.ApiKey);
            }

            var kernel = builder.Build();
            return kernel;
        });

        // 注册 AI 服务实现
        _ = services.AddKeyedTransient<IXiHanAIService, XiHanOllamaService>("Ollama");
        _ = services.AddKeyedTransient<IXiHanAIService, XiHanOpenAIService>("OpenAI");
    }
}

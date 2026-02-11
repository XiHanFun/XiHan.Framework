#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIModule
// Guid:60bb5691-f03a-4596-84cb-8600003b8fd9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:27:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using XiHan.Framework.AI.Extensions.DependencyInjection;
using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Providers;
using XiHan.Framework.AI.Providers.Ollama;
using XiHan.Framework.AI.Providers.OpenAI;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Http;

namespace XiHan.Framework.AI;

#pragma warning disable SKEXP0010

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
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanAI(config);

        // 注册 Semantic Kernel 内核，配置 Ollama、OpenAI 等 Connector
        var kernelBuilder = services.AddKernel();

        // Ollama
        PostConfigure<OllamaOptions>(ollamaOptions =>
        {
            kernelBuilder.AddOllamaChatCompletion(
                modelId: ollamaOptions.ModelName,
                endpoint: new Uri(ollamaOptions.BaseUrl),
                serviceId: ollamaOptions.ServiceId);
            kernelBuilder.AddOllamaEmbeddingGenerator(
                modelId: ollamaOptions.ModelName,
                endpoint: new Uri(ollamaOptions.BaseUrl),
                serviceId: ollamaOptions.ServiceId);
            services.AddKeyedTransient<IXiHanAIService, XiHanOllamaService>(ollamaOptions.ServiceId);
        });
        // OpenAI
        PostConfigure<OpenAIOptions>(openAiOptions =>
        {
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: openAiOptions.ModelName,
                endpoint: new Uri(openAiOptions.BaseUrl),
                apiKey: openAiOptions.ApiKey,
                serviceId: openAiOptions.ServiceId);
            kernelBuilder.AddOpenAIEmbeddingGenerator(
                modelId: openAiOptions.ModelName,
                apiKey: openAiOptions.ApiKey,
                serviceId: openAiOptions.ServiceId);
            services.AddKeyedTransient<IXiHanAIService, XiHanOpenAIService>(openAiOptions.ServiceId);
        });
    }
}

#pragma warning restore SKEXP0010

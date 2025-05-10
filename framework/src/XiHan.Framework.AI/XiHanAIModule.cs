#region <<版权版本注释>>

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
using XiHan.Framework.Http;

namespace XiHan.Framework.AI;

/// <summary>
/// 曦寒框架人工智能模块
/// </summary>
[DependsOn(
    typeof(XiHanHttpModule)
    )]
public class XiHanAiModule : XiHanModule
{
    private const string ModuleConfigNode = "XiHan:AI";

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        Configure<XiHanOllamaOptions>(configuration.GetSection($"{ModuleConfigNode}:Ollama"));
        Configure<XiHanOpenAiOptions>(configuration.GetSection($"{ModuleConfigNode}:OpenAI"));

        // 注册 Semantic Kernel 内核，配置 Ollama、OpenAI 等 Connector
        var kernelBuilder = services.AddKernel();

        // Ollama
        var ollamaOptions = services.GetRequiredService<IOptions<XiHanOllamaOptions>>().Value;
#pragma warning disable SKEXP0070
        _ = kernelBuilder.AddOllamaChatCompletion(
            modelId: ollamaOptions.ModelId,
            endpoint: new Uri(ollamaOptions.Endpoint),
            serviceId: ollamaOptions.ServiceId);
        _ = kernelBuilder.AddOllamaTextEmbeddingGeneration(
            modelId: ollamaOptions.ModelId,
            endpoint: new Uri(ollamaOptions.Endpoint),
            serviceId: ollamaOptions.ServiceId);
#pragma warning restore SKEXP0070

        _ = services.AddKeyedTransient<IXiHanAiService, XiHanOllamaService>(ollamaOptions.ServiceId);

        // OpenAI
        var openAiOptions = services.GetRequiredService<IOptions<XiHanOpenAiOptions>>().Value;
#pragma warning disable SKEXP0010
        _ = kernelBuilder.AddOpenAIChatCompletion(
            modelId: openAiOptions.ModelId,
            endpoint: new Uri(openAiOptions.Endpoint),
            apiKey: openAiOptions.ApiKey,
            serviceId: openAiOptions.ServiceId);
        _ = kernelBuilder.AddOpenAITextEmbeddingGeneration(
            modelId: openAiOptions.ModelId,
            apiKey: openAiOptions.ApiKey,
            serviceId: openAiOptions.ServiceId);
#pragma warning restore SKEXP0010

        _ = services.AddKeyedTransient<IXiHanAiService, XiHanOpenAiService>(openAiOptions.ServiceId);

        _ = kernelBuilder.Build();
    }
}

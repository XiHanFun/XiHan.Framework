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
using XiHan.Framework.AI.Providers.HuggingFace;
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
public class XiHanAIModule : XiHanModule
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
        Configure<XiHanHuggingFaceOptions>(configuration.GetSection($"{ModuleConfigNode}:HuggingFace"));
        Configure<XiHanOpenAIOptions>(configuration.GetSection($"{ModuleConfigNode}:OpenAI"));

        // 注册 Semantic Kernel 内核，配置 Ollama、HuggingFace、OpenAI 等 Connector
        _ = services.AddSingleton<Kernel>(provider =>
        {
            var builder = Kernel.CreateBuilder();

            // Ollama
            var ollamaOptions = services.GetRequiredService<IOptions<XiHanOllamaOptions>>().Value;
            if (ollamaOptions is not null)
            {
#pragma warning disable SKEXP0070
                _ = builder.AddOllamaChatCompletion(
                        modelId: ollamaOptions.ModelId,
                        endpoint: new Uri(ollamaOptions.Endpoint),
                        serviceId: ollamaOptions.ServiceId);
                _ = builder.AddOllamaTextEmbeddingGeneration(
                        modelId: ollamaOptions.ModelId,
                        endpoint: new Uri(ollamaOptions.Endpoint),
                        serviceId: ollamaOptions.ServiceId);
                _ = builder.AddOllamaTextGeneration(
                        modelId: ollamaOptions.ModelId,
                        endpoint: new Uri(ollamaOptions.Endpoint),
                        serviceId: ollamaOptions.ServiceId);
#pragma warning restore SKEXP0070

                _ = services.AddKeyedTransient<IXiHanAIService, XiHanOllamaService>(ollamaOptions.ServiceId);
            }

            // HuggingFace
            var huggingFaceOptions = services.GetRequiredService<IOptions<XiHanHuggingFaceOptions>>().Value;
            if (huggingFaceOptions is not null)
            {
#pragma warning disable SKEXP0070
                _ = builder.AddHuggingFaceChatCompletion(
                        model: huggingFaceOptions.ModelId,
                        endpoint: new Uri(huggingFaceOptions.Endpoint),
                        apiKey: huggingFaceOptions.ApiKey,
                        serviceId: huggingFaceOptions.ServiceId);
                _ = builder.AddHuggingFaceImageToText(
                        model: huggingFaceOptions.ModelId,
                        endpoint: new Uri(huggingFaceOptions.Endpoint),
                        apiKey: huggingFaceOptions.ApiKey,
                        serviceId: huggingFaceOptions.ServiceId);
                _ = builder.AddHuggingFaceTextEmbeddingGeneration(
                        model: huggingFaceOptions.ModelId,
                        endpoint: new Uri(huggingFaceOptions.Endpoint),
                        apiKey: huggingFaceOptions.ApiKey,
                        serviceId: huggingFaceOptions.ServiceId);
                _ = builder.AddHuggingFaceTextGeneration(
                        model: huggingFaceOptions.ModelId,
                        endpoint: new Uri(huggingFaceOptions.Endpoint),
                        apiKey: huggingFaceOptions.ApiKey,
                        serviceId: huggingFaceOptions.ServiceId);
#pragma warning restore SKEXP0070

                _ = services.AddKeyedTransient<IXiHanAIService, XiHanHuggingFaceService>(huggingFaceOptions.ServiceId);
            }

            // OpenAI
            var openAIOptions = services.GetRequiredService<IOptions<XiHanOpenAIOptions>>().Value;
            if (openAIOptions is not null)
            {
#pragma warning disable SKEXP0010
                _ = builder.AddOpenAIAudioToText(
                        modelId: openAIOptions.ModelId,
                        apiKey: openAIOptions.ApiKey,
                        serviceId: openAIOptions.ServiceId);
                _ = builder.AddOpenAIChatCompletion(
                        modelId: openAIOptions.ModelId,
                        endpoint: new Uri(openAIOptions.Endpoint),
                        apiKey: openAIOptions.ApiKey,
                        serviceId: openAIOptions.ServiceId);
                _ = builder.AddOpenAITextEmbeddingGeneration(
                        modelId: openAIOptions.ModelId,
                        apiKey: openAIOptions.ApiKey,
                        serviceId: openAIOptions.ServiceId);
                _ = builder.AddOpenAITextToAudio(
                        modelId: openAIOptions.ModelId,
                        apiKey: openAIOptions.ApiKey,
                        serviceId: openAIOptions.ServiceId);
                _ = builder.AddOpenAITextToImage(
                        modelId: openAIOptions.ModelId,
                        apiKey: openAIOptions.ApiKey,
                        serviceId: openAIOptions.ServiceId);
#pragma warning restore SKEXP0010

                _ = services.AddKeyedTransient<IXiHanAIService, XiHanOpenAIService>(openAIOptions.ServiceId);
            }

            var kernel = builder.Build();
            return kernel;
        });
    }
}

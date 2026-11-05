// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.OpenAI;

/// <summary>
/// OpenAI 兼容端点的 Provider
/// </summary>
/// <remarks>
/// 提供 <c>ai.chat@1</c> 与 <c>ai.embedding@1</c>；装配与启动不触网，客户端在首次解析时创建。启动时构建工具目录并解析提示增强器：工具名不合法或重复、知识库配置引用的提示词找不到或渲染不了即启动失败——只装本特性（不装工具目录特性与知识库 Provider）时也一样
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OpenAIFeature : IHostedFeature
{
    /// <summary>
    /// 发请求用的命名 HttpClient
    /// </summary>
    /// <remarks>
    /// 容器里有 <see cref="IHttpClientFactory"/> 时经这个名字的客户端发请求。框架的端点来自配置，不过出站地址策略（ADR-0117）；应用的提供商目录让用户填端点时，给这个名字接上策略：<c>services.AddHttpClient(OpenAIFeature.HttpClientName).UseSocketsHttpHandler(static (handler, sp) => sp.GetRequiredService&lt;OutboundAddressPolicy&gt;().Apply(handler))</c>
    /// </remarks>
    public const string HttpClientName = "xihan.ai.openai";

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.ai.openai",
        Package = "XiHan.Framework.AI.OpenAI",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides =
        [
            new CapabilityDeclaration(AICapabilities.Chat, AICapabilities.Version),
            new CapabilityDeclaration(AICapabilities.Embedding, AICapabilities.Version),
        ],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution(AIOptions.ConfigurationSection) },
        Entry = typeof(OpenAIFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddAI(context.Configuration);
        context.Services.AddAIChatClientFactory<OpenAIChatClientFactory>();
        context.Services.AddAIEmbeddingGeneratorFactory<OpenAIEmbeddingGeneratorFactory>();
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 聊天用工具目录：只装本特性时没有工具目录特性在启动时构建它，不构建就要推迟到第一次聊天才暴露工具名不合法或重名
        services.GetRequiredService<IAIToolCatalog>();
        // 提示增强器创建时校验知识库配置引用的提示词：只装本特性时没有别的 AI 特性在启动时解析它，不解析就要推迟到第一次检索增强才失败
        services.GetRequiredService<IRagPromptAugmenter>();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

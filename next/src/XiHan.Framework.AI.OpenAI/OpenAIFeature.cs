// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.OpenAI;

/// <summary>
/// OpenAI 兼容端点的 Provider：提供 <c>ai.chat@1</c> 与 <c>ai.embedding@1</c>；装配与启动不触网，客户端在首次解析时创建。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OpenAIFeature : IFeature
{
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
}

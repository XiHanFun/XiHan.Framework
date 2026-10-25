// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.Conformance;

/// <summary>
/// 套件用的 <c>ai.embedding@1</c> Provider：登记 <see cref="HashingEmbeddingGeneratorFactory"/>，让知识库 Provider 的测试不依赖外部模型。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HashingEmbeddingFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.ai.hashing",
        Package = "XiHan.Framework.AI.Conformance",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(AICapabilities.Embedding, AICapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution(AIOptions.ConfigurationSection) },
        Entry = typeof(HashingEmbeddingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddAI(context.Configuration);
        context.Services.AddSingleton<HashingEmbeddingGeneratorFactory>();
        context.Services.AddSingleton<IEmbeddingGeneratorFactory>(static sp => sp.GetRequiredService<HashingEmbeddingGeneratorFactory>());
    }
}

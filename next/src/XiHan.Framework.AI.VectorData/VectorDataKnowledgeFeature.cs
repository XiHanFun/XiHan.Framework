// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.VectorData;

/// <summary>
/// 知识库 Provider：提供 <c>ai.knowledge@1</c>，把 <see cref="IKnowledgeStore"/> 映射到应用注册的 <see cref="VectorStore"/>；启动时容器里没有 <see cref="VectorStore"/> → <see cref="AIErrors.KnowledgeUnavailable"/> 启动失败。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class VectorDataKnowledgeFeature : IHostedFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.ai.vectordata",
        Package = "XiHan.Framework.AI.VectorData",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(AICapabilities.Knowledge, AICapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(AICapabilities.Embedding, AICapabilities.Version),
        ],
        Contributes = new Contributions { Config = new ConfigContribution(AIOptions.ConfigurationSection) },
        Entry = typeof(VectorDataKnowledgeFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddAI(context.Configuration);
        if (context.Node.IsSelectedFor(AICapabilities.Knowledge))
        {
            context.Services.TryAddSingleton<IKnowledgeStore, VectorDataKnowledgeStore>();
        }
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 向量存储由应用选连接器注册；没有就是装配错误，启动即失败而不是第一次检索时
        if (services.GetService<VectorStore>() is null)
        {
            throw new FrameworkException(AIErrors.KnowledgeUnavailable, "No 'Microsoft.Extensions.VectorData.VectorStore' is registered; register a vector store connector before using 'xihan.ai.vectordata'.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

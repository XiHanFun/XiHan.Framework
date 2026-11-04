// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.VectorData;

/// <summary>
/// 知识库 Provider
/// </summary>
/// <remarks>
/// 提供 <c>ai.knowledge@1</c>，把 <see cref="IKnowledgeStore"/> 映射到应用注册的 <see cref="VectorStore"/>；启动时容器里没有 <see cref="VectorStore"/> → <see cref="AIErrors.KnowledgeUnavailable"/> 启动失败；同时解析提示增强器，知识库配置引用的提示词找不到或渲染不了即启动失败
/// 被选为 <c>ai.knowledge</c> 时带健康项 <see cref="HealthName"/>：按 <see cref="VectorDataKnowledgeOptions.HealthProbeInterval"/> 周期问向量存储默认集合是否存在，连接器抛出时 Degraded（<see cref="AIErrors.KnowledgeUnavailable"/>），超时 Degraded（<c>KERNEL_UNAVAILABLE_005</c>），应用照常服务
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class VectorDataKnowledgeFeature : IHostedFeature
{
    /// <summary>
    /// 健康项名：向量存储是否应答
    /// </summary>
    public const string HealthName = "ai.vectordata.store";

    /// <summary>
    /// 本特性是否被选为 <c>ai.knowledge</c>
    /// </summary>
    /// <remarks>
    /// 由 <see cref="Configure"/> 判定；没被选中时不登记存储与健康项，启动时也不探测
    /// </remarks>
    private bool _providesKnowledge;

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它
    /// </summary>
    private PeriodicHealthContributor? _health;

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
        Contributes = new Contributions
        {
            Config = new ConfigContribution(AIOptions.ConfigurationSection),
            Health = [HealthName],
        },
        Entry = typeof(VectorDataKnowledgeFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddAI(context.Configuration);
        _providesKnowledge = context.Node.IsSelectedFor(AICapabilities.Knowledge);
        if (!_providesKnowledge)
        {
            return;
        }

        // 选项值不合法即在装配时失败，不等到启动
        var options = VectorDataKnowledgeOptions.FromConfiguration(context.Configuration);
        context.Services.TryAddSingleton(options);
        context.Services.TryAddSingleton<VectorDataKnowledgeStore>();
        context.Services.TryAddSingleton<IKnowledgeStore>(static sp => sp.GetRequiredService<VectorDataKnowledgeStore>());
        // 健康项按名字作键登记：探针在根容器上取存储，不开作用域、不经管道；内核经 IHealthContributor 统一采集
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, ct => sp.GetRequiredService<VectorDataKnowledgeStore>().ProbeAsync(ct), options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 向量存储由应用选连接器注册；没有就是装配错误，启动即失败而不是第一次检索时
        if (services.GetService<VectorStore>() is null)
        {
            throw new FrameworkException(AIErrors.KnowledgeUnavailable, "No 'Microsoft.Extensions.VectorData.VectorStore' is registered; register a vector store connector before using 'xihan.ai.vectordata'.");
        }

        // 提示增强器创建时校验知识库配置引用的提示词：找不到或渲染不了在启动时失败，而不是第一次检索增强时
        services.GetRequiredService<IRagPromptAugmenter>();

        // 装配错误都查过之后才起健康循环：前面失败时不留下在跑的循环。首探同步完成，向量库连不上只让条目 Degraded，不让启动失败
        if (_providesKnowledge)
        {
            _health = services.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName);
            await _health.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_health is not null)
        {
            await _health.StopAsync(cancellationToken).ConfigureAwait(false);
            _health = null;
        }
    }
}

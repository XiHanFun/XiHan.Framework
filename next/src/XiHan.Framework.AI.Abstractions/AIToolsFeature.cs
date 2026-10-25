// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.AI.Abstractions.Tools;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 工具目录特性（<c>role: default</c>）：提供 <c>ai.tools@1</c>——进程内、零外部依赖；启动时构建目录，工具名不合法或重复即启动失败。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AIToolsFeature : IHostedFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.ai.tools",
        Package = "XiHan.Framework.AI.Abstractions",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Capability,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(AICapabilities.Tools, AICapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution(AIOptions.ConfigurationSection) },
        Entry = typeof(AIToolsFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddAI(context.Configuration);
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 启动即构建：工具名问题在启动时暴露，而不是第一次聊天时
        services.GetRequiredService<IAIToolCatalog>();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

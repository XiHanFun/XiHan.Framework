// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Auditing.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Auditing.InMemory;

/// <summary>
/// 内存审计的能力包入口。
/// </summary>
/// <remarks>
/// 默认提供 <c>auditing.trail@1</c>。被选中时把 <see cref="InMemoryAuditTrail"/> 绑定为接收器与查询，并挂上审计中间件。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryAuditingFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.auditing.inmemory",
        Package = "XiHan.Framework.Auditing.InMemory",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(AuditingCapabilities.Trail, AuditingCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Auditing"),
        },
        Entry = typeof(InMemoryAuditingFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton(InMemoryAuditingOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<InMemoryAuditTrail>();
        if (context.Node.IsSelectedFor(AuditingCapabilities.Trail))
        {
            // 特性在内核的默认注册之前配置，所以 TryAdd 能先于丢弃实现占住 IAuditSink
            context.Services.TryAddSingleton<IAuditSink>(static sp => sp.GetRequiredService<InMemoryAuditTrail>());
            context.Services.TryAddSingleton<IAuditTrail>(static sp => sp.GetRequiredService<InMemoryAuditTrail>());
            context.AddAuditing();
        }
    }
}

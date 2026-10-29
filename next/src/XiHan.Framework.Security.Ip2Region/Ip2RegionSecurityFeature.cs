// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Network;

namespace XiHan.Framework.Security.Ip2Region;

/// <summary>
/// ip2region 归属地的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>security.ip-region@1</c>，以 <see cref="Ip2RegionResolver"/> 单例实现 <see cref="IIpRegionResolver"/>。xdb 文件由宿主提供，路径见 <see cref="Ip2RegionOptions"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class Ip2RegionSecurityFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.security.ip2region",
        Package = "XiHan.Framework.Security.Ip2Region",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(SecurityCapabilities.IpRegion, SecurityCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:Security"),
        },
        Entry = typeof(Ip2RegionSecurityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddSingleton(Ip2RegionOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<IIpRegionResolver, Ip2RegionResolver>();
    }
}

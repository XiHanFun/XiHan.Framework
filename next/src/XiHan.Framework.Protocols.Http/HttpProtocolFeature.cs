// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// HTTP 协议适配器的能力包入口。
/// </summary>
/// <remarks>
/// 提供 <c>protocols.http@1</c>，注册选项；端点由应用在路由阶段调用 <c>MapXiHanOperations()</c> 映射。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class HttpProtocolFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.protocols.http",
        Package = "XiHan.Framework.Protocols.Http",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Stable,
        Kind = ManifestKind.Protocol,
        Provides = [new CapabilityDeclaration(HttpProtocolCapabilities.Http, HttpProtocolCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        // 清单只声明领域级配置节 XiHan:Protocols；本适配器读其下的 Http 子节
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Protocols") },
        Entry = typeof(HttpProtocolFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(HttpMessages.Catalog);
        context.Services.AddSingleton(HttpProtocolOptions.FromConfiguration(context.Configuration));
    }
}

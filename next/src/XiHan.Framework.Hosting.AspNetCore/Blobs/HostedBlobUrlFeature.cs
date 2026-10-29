// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Hosting.AspNetCore.Blobs;

/// <summary>
/// 宿主代收发的预签名地址（能力 <c>objectstorage.presign@1</c>）。
/// </summary>
/// <remarks>
/// 给没有原生签名的对象存储（文件系统）装上 <see cref="HostedBlobUrlSigner"/>，地址由应用在路由阶段 <c>MapXiHanBlobs()</c> 映射的端点收发。存储本身能签名（S3）时不要装：两个 Provider 都提供同一能力，按显式 Provider 的选择规则只有一个生效。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HostedBlobUrlFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.hosting.blob-urls",
        Package = "XiHan.Framework.Hosting.AspNetCore",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides = [new CapabilityDeclaration(ObjectStorageCapabilities.Presign, ObjectStorageCapabilities.Version)],
        Requires =
        [
            new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version),
            new CapabilityRequirement(ObjectStorageCapabilities.Blob, ObjectStorageCapabilities.Version),
        ],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:ObjectStorage") },
        Entry = typeof(HostedBlobUrlFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(ObjectStorageMessages.Catalog);
        context.Services.AddSingleton(HostedBlobUrlOptions.FromConfiguration(context.Configuration));
        context.Services.TryAddSingleton<HostedBlobUrlSigner>();
        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Presign))
        {
            context.Services.TryAddSingleton<IBlobUrlSigner>(static sp => sp.GetRequiredService<HostedBlobUrlSigner>());
        }
    }
}

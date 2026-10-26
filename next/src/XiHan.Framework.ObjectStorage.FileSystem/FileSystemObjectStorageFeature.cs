// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 文件系统对象存储的能力包入口：默认提供 <c>objectstorage.blob@1</c>；启动时建根目录并扫描已用量。另有显式 Provider 被选中时只注册具体类型，不绑定契约。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class FileSystemObjectStorageFeature : IHostedFeature
{
    /// <summary>
    /// 健康项名：总配额是否还有余量。
    /// </summary>
    public const string HealthName = "objectstorage.filesystem.capacity";

    /// <summary>
    /// 启动后保存的存储实例，供停止时使用。
    /// </summary>
    private FileSystemBlobStore? _store;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.objectstorage.filesystem",
        Package = "XiHan.Framework.ObjectStorage.FileSystem",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(ObjectStorageCapabilities.Blob, ObjectStorageCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:ObjectStorage"),
            Health = [HealthName],
        },
        Entry = typeof(FileSystemObjectStorageFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(ObjectStorageMessages.Catalog);
        context.Services.AddSingleton(FileSystemObjectStorageOptions.FromConfiguration(context.Configuration));
        context.Services.AddSingleton<FileSystemBlobStore>();
        // 存储自己就是健康项的贡献者：直接看已用量与总配额
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<FileSystemBlobStore>());
        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Blob))
        {
            context.Services.TryAddSingleton<IBlobStore>(static sp => sp.GetRequiredService<FileSystemBlobStore>());
        }
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _store = services.GetRequiredService<FileSystemBlobStore>();
        return _store.InitializeAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _store = null;
        return Task.CompletedTask;
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 文件系统对象存储的能力包入口
/// </summary>
/// <remarks>
/// 默认提供 <c>objectstorage.blob@1</c> 与 <c>objectstorage.multipart@1</c>（分片暂存在容器的 <c>uploads</c> 目录）；启动时建根目录、扫描已用量并清理过期的分片上传。预签名地址由宿主的签名端点代为收发（<c>Hosting.AspNetCore</c> 的 <c>HostedBlobUrlFeature</c>）。被要求提供分片上传时必须同时被选为 <c>objectstorage.blob</c>（缺省的三个契约落在同一存储上，<see cref="BlobStoreSelection"/>）。命名实例的 Provider <see cref="FileSystemBlobStoreProvider"/>（名 <c>filesystem</c>）总是登记：能力全部被别的存储顶替，或没被选为 <c>objectstorage.blob</c> 而分片上传只是兜底选出时，只登记它（<see cref="ConfigureShadowed"/>），不建缺省实例、不启动
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class FileSystemObjectStorageFeature : IHostedFeature, IShadowAwareFeature
{
    /// <summary>
    /// 健康项名：总配额是否还有余量
    /// </summary>
    public const string HealthName = "objectstorage.filesystem.capacity";

    /// <summary>
    /// 启动后保存的存储实例，供停止时使用
    /// </summary>
    private FileSystemBlobStore? _store;

    /// <summary>
    /// 本特性是否提供缺省实例
    /// </summary>
    /// <remarks>
    /// 由 <see cref="Configure"/> 判定（<see cref="BlobStoreSelection.ProvidesDefaultInstance"/>）；不提供时与被顶替相同，启动时不建根目录、不扫描
    /// </remarks>
    private bool _providesDefault;

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
        Provides =
        [
            new CapabilityDeclaration(ObjectStorageCapabilities.Blob, ObjectStorageCapabilities.Version),
            new CapabilityDeclaration(ObjectStorageCapabilities.Multipart, ObjectStorageCapabilities.Version),
        ],
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
        // 命名实例：按定义（配置 XiHan:ObjectStorage:Stores 或应用运行时登记）各用各的根目录，与是否提供缺省实例无关
        context.Services.AddBlobStoreProvider<FileSystemBlobStoreProvider>(context.Configuration);
        // 没被选为缺省对象存储、分片上传也只是兜底选出：没有人会经本包取用缺省实例，与被顶替相同，到此为止；
        // 被要求却与缺省对象存储不一致时这里启动失败
        _providesDefault = BlobStoreSelection.ProvidesDefaultInstance(context.Node);
        if (!_providesDefault)
        {
            return;
        }

        context.Services.AddSingleton(FileSystemObjectStorageOptions.FromConfiguration(context.Configuration));
        // 构造不公开：特性在 StartAsync 里初始化它（扫描已用量），再交给容器里的使用方
        context.Services.AddSingleton(static sp => new FileSystemBlobStore(sp.GetRequiredService<FileSystemObjectStorageOptions>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        // 存储自己就是健康项的贡献者：直接看已用量与总配额
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredService<FileSystemBlobStore>());
        // 走到这里本包已被选为 objectstorage.blob，是缺省存储；分片上传被选中就注册（兜底选出的也注册）
        context.Services.TryAddSingleton<IBlobStore>(static sp => sp.GetRequiredService<FileSystemBlobStore>());
        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Multipart))
        {
            context.Services.TryAddSingleton<IBlobMultipartStore>(static sp => sp.GetRequiredService<FileSystemBlobStore>());
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 只登记命名实例的 Provider：缺省实例由别的存储提供，不读本包的配置节、不建根目录
    /// </remarks>
    public void ConfigureShadowed(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(ObjectStorageMessages.Catalog);
        context.Services.AddBlobStoreProvider<FileSystemBlobStoreProvider>(context.Configuration);
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!_providesDefault)
        {
            return Task.CompletedTask;
        }

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

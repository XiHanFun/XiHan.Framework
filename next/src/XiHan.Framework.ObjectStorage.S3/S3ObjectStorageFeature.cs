// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储的能力包入口。
/// </summary>
/// <remarks>
/// 显式 Provider，提供 <c>objectstorage.blob@1</c>、<c>objectstorage.multipart@1</c> 与 <c>objectstorage.presign@1</c>（原生分片与预签名，客户端直连存储）。客户端由本特性按选项创建（应用已注册 <see cref="IAmazonS3"/> 时沿用），停止时释放。健康项 <see cref="HealthName"/> 按 <see cref="S3ObjectStorageOptions.HealthProbeInterval"/> 周期对 <see cref="S3ObjectStorageOptions.HealthBucket"/> HEAD（没配时列桶）。另登记命名实例的 Provider <see cref="S3BlobStoreProvider"/>（名 <c>s3</c>）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3ObjectStorageFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名。
    /// </summary>
    /// <remarks>
    /// <c>objectstorage</c>，类型 <c>storage:object</c>，可选。主值是服务端点，属性 <c>region</c>、<c>accessKey</c>、<c>secretKey</c>、<c>forcePathStyle</c>。
    /// </remarks>
    public const string ConnectionBinding = "objectstorage";

    /// <summary>
    /// 健康项名：服务是否可达。
    /// </summary>
    public const string HealthName = "objectstorage.s3.connection";

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它。
    /// </summary>
    private PeriodicHealthContributor? _health;

    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.objectstorage.s3",
        Package = "XiHan.Framework.ObjectStorage.S3",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Provider,
        Provides =
        [
            new CapabilityDeclaration(ObjectStorageCapabilities.Blob, ObjectStorageCapabilities.Version),
            new CapabilityDeclaration(ObjectStorageCapabilities.Multipart, ObjectStorageCapabilities.Version),
            new CapabilityDeclaration(ObjectStorageCapabilities.Presign, ObjectStorageCapabilities.Version),
        ],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Bindings = [new Binding(ConnectionBinding, "storage:object", required: false)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:ObjectStorage"),
            Health = [HealthName],
        },
        Entry = typeof(S3ObjectStorageFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(ObjectStorageMessages.Catalog);
        var options = S3ObjectStorageOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding));
        context.Services.AddSingleton(options);
        // 应用自己注册的客户端优先（自定义凭据链、代理、重试策略）
        context.Services.TryAddSingleton<IAmazonS3>(_ => CreateClient(options));
        context.Services.TryAddSingleton<S3BlobStore>();
        // 命名实例：按定义（配置 XiHan:ObjectStorage:Stores 或应用运行时登记）各建各的客户端
        context.Services.AddBlobStoreProvider<S3BlobStoreProvider>(context.Configuration);
        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Blob))
        {
            context.Services.TryAddSingleton<IBlobStore>(static sp => sp.GetRequiredService<S3BlobStore>());
        }

        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Multipart))
        {
            context.Services.TryAddSingleton<IBlobMultipartStore>(static sp => sp.GetRequiredService<S3BlobStore>());
        }

        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Presign))
        {
            context.Services.TryAddSingleton<IBlobUrlSigner>(static sp => sp.GetRequiredService<S3BlobStore>());
        }

        // 健康项按名字作键登记：几个 Provider 的探针各自独立，内核经 IHealthContributor 统一采集
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, ct => ProbeAsync(sp.GetRequiredService<IAmazonS3>(), options.HealthBucket, ct), options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddSingleton<IHealthContributor>(static sp => sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName));
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _health = services.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName);
        await _health.StartAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// 按选项创建客户端。
    /// </summary>
    /// <remarks>
    /// 有端点时指向兼容服务并只在服务端要求时计算 / 校验校验和（多数兼容服务不支持分块尾部校验和），没有端点时按区域访问 Amazon S3；没有密钥时走 SDK 默认凭据链。
    /// </remarks>
    public static AmazonS3Client CreateClient(S3ObjectStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle };
        if (options.ServiceUrl is { } url)
        {
            config.ServiceURL = url;
            config.AuthenticationRegion = options.Region;
            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
            config.ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        return options.AccessKey is { } accessKey && options.SecretKey is { } secretKey
            ? new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config)
            : new AmazonS3Client(config);
    }

    /// <summary>
    /// 连通性探针。
    /// </summary>
    /// <remarks>
    /// 配了健康桶就 HEAD 它（桶不存在、无权限或服务不可达都抛出）；没配就列桶，是最轻的通用管理调用。异常由探针宿主记成 Degraded 并带异常消息。
    /// </remarks>
    internal static async ValueTask<Result> ProbeAsync(IAmazonS3 client, string? bucket, CancellationToken cancellationToken)
    {
        if (bucket is not null)
        {
            await client.HeadBucketAsync(new HeadBucketRequest { BucketName = bucket }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await client.ListBucketsAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}

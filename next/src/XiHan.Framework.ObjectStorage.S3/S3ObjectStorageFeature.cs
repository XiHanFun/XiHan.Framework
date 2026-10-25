// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储的能力包入口：显式 Provider，提供 <c>objectstorage.blob@1</c>。客户端由本特性按选项创建（应用已注册 <see cref="IAmazonS3"/> 时沿用），停止时释放。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3ObjectStorageFeature : IHostedFeature
{
    /// <summary>
    /// 连接绑定名：<c>objectstorage</c>，类型 <c>storage:object</c>，可选。主值是服务端点，属性 <c>region</c>、<c>accessKey</c>、<c>secretKey</c>、<c>forcePathStyle</c>。
    /// </summary>
    public const string ConnectionBinding = "objectstorage";

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
        Provides = [new CapabilityDeclaration(ObjectStorageCapabilities.Blob, ObjectStorageCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Bindings = [new Binding(ConnectionBinding, "storage:object", required: false)],
        Contributes = new Contributions
        {
            Config = new ConfigContribution("XiHan:ObjectStorage"),
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
        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Blob))
        {
            context.Services.TryAddSingleton<IBlobStore>(static sp => sp.GetRequiredService<S3BlobStore>());
        }
    }

    /// <inheritdoc />
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 按选项创建客户端：有端点时指向兼容服务并只在服务端要求时计算 / 校验校验和（多数兼容服务不支持分块尾部校验和），没有端点时按区域访问 Amazon S3；没有密钥时走 SDK 默认凭据链。
    /// </summary>
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
}

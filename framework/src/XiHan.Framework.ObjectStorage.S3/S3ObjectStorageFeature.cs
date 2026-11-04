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
/// S3 对象存储的能力包入口
/// </summary>
/// <remarks>
/// 显式 Provider，提供 <c>objectstorage.blob@1</c>、<c>objectstorage.multipart@1</c> 与 <c>objectstorage.presign@1</c>（原生分片与预签名，客户端直连存储）。客户端由本特性按选项创建（应用已注册 <see cref="IAmazonS3"/> 时沿用），停止时释放。启动时探一次服务是否认条件写入（<see cref="S3ObjectStorageOptions.ConditionalWrites"/>，与健康项的首次探测并行，ADR-0089、ADR-0090、ADR-0093）：探明不认、被拒绝或探测桶不存在都启动失败；只有连不上照常启动，在后台按退避重试，探明之前不覆盖写入返回 <c>OBJECTSTORAGE_UNAVAILABLE_004</c>。健康项 <see cref="HealthName"/> 按 <see cref="S3ObjectStorageOptions.HealthProbeInterval"/> 周期对 <see cref="S3ObjectStorageOptions.HealthBucket"/> HEAD（没配时列桶），并即时并入条件写入的状态（数据 <c>conditionalWrites</c>；连不上 Degraded，不认以及启动之后才被拒绝或缺桶 Failed）。被要求提供分片上传或预签名时必须同时被选为 <c>objectstorage.blob</c>（缺省的三个契约落在同一存储上，<see cref="BlobStoreSelection"/>）。命名实例的 Provider <see cref="S3BlobStoreProvider"/>（名 <c>s3</c>）总是登记：能力全部被别的存储顶替，或没被选为 <c>objectstorage.blob</c> 而其余能力都只是兜底选出时，只登记它（被顶替时经 <see cref="ConfigureShadowed"/>），不建缺省客户端、不探测
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3ObjectStorageFeature : IHostedFeature, IShadowAwareFeature
{
    /// <summary>
    /// 连接绑定名
    /// </summary>
    /// <remarks>
    /// <c>objectstorage</c>，类型 <c>blob</c>（RFC-0016 的绑定类型词表），可选。主值是服务端点，属性 <c>region</c>、<c>accessKey</c>、<c>secretKey</c>、<c>forcePathStyle</c>
    /// </remarks>
    public const string ConnectionBinding = "objectstorage";

    /// <summary>
    /// 健康项名：服务是否可达
    /// </summary>
    public const string HealthName = "objectstorage.s3.connection";

    /// <summary>
    /// 启动时起循环的健康探针，停止时结束它
    /// </summary>
    private PeriodicHealthContributor? _health;

    /// <summary>
    /// 缺省实例的条件写入状态：启动时探一次、停止时结束后台重试
    /// </summary>
    private S3ConditionalWriteGate? _conditionalWrites;

    /// <summary>
    /// 本特性是否提供缺省实例
    /// </summary>
    /// <remarks>
    /// 由 <see cref="Configure"/> 判定（<see cref="BlobStoreSelection.ProvidesDefaultInstance"/>）；不提供时与被顶替相同，启动时不探测
    /// </remarks>
    private bool _providesDefault;

    /// <summary>
    /// 本特性是否提供缺省的分片上传
    /// </summary>
    /// <remarks>
    /// 提供时启动探测也核对分片上传的条件完成
    /// </remarks>
    private bool _providesMultipart;

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
        Bindings = [new Binding(ConnectionBinding, "blob", required: false)],
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
        // 命名实例：按定义（配置 XiHan:ObjectStorage:Stores 或应用运行时登记）各建各的客户端，与是否提供缺省实例无关
        context.Services.AddBlobStoreProvider<S3BlobStoreProvider>(context.Configuration);
        // 没被选为缺省对象存储、分片上传与预签名也只是兜底选出：没有人会经本包取用缺省实例，与被顶替相同，到此为止；
        // 被要求却与缺省对象存储不一致时这里启动失败
        _providesDefault = BlobStoreSelection.ProvidesDefaultInstance(context.Node);
        if (!_providesDefault)
        {
            return;
        }

        var options = S3ObjectStorageOptions.FromConfiguration(context.Configuration, context.TryGetBinding(ConnectionBinding));
        // 条件写入要在启动时探测，探测要有桶：没配就是装配错误，不等到启动再发现
        S3ConditionalWriteProbe.RequireBucket(options, S3ObjectStorageOptions.ConfigurationSection + ":");
        context.Services.AddSingleton(options);
        // 应用自己注册的客户端优先（自定义凭据链、代理、重试策略）
        context.Services.TryAddSingleton<IAmazonS3>(_ => CreateClient(options));
        _providesMultipart = context.Node.IsSelectedFor(ObjectStorageCapabilities.Multipart);
        // 条件写入的状态是缺省实例与健康项共用的单例；提供分片上传时探测也核对分片的条件完成
        var multipart = _providesMultipart;
        context.Services.TryAddSingleton(sp => S3ConditionalWriteGate.Create(sp.GetRequiredService<IAmazonS3>(), options, multipart, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>().CreateLogger<S3ConditionalWriteGate>(), "default"));
        context.Services.TryAddSingleton(sp => new S3BlobStore(sp.GetRequiredService<IAmazonS3>(), options, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<S3ConditionalWriteGate>(), owned: false));
        // 走到这里本包已被选为 objectstorage.blob，是缺省存储；其余能力被选中就注册（兜底选出的也注册）
        context.Services.TryAddSingleton<IBlobStore>(static sp => sp.GetRequiredService<S3BlobStore>());
        if (_providesMultipart)
        {
            context.Services.TryAddSingleton<IBlobMultipartStore>(static sp => sp.GetRequiredService<S3BlobStore>());
        }

        if (context.Node.IsSelectedFor(ObjectStorageCapabilities.Presign))
        {
            context.Services.TryAddSingleton<IBlobUrlSigner>(static sp => sp.GetRequiredService<S3BlobStore>());
        }

        // 健康项按名字作键登记：几个 Provider 的探针各自独立，内核经 IHealthContributor 统一采集
        context.Services.AddKeyedSingleton(HealthName, (sp, _) => new PeriodicHealthContributor(HealthName, ct => ProbeAsync(sp.GetRequiredService<IAmazonS3>(), options.HealthBucket, ct), options.HealthProbeInterval, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILoggerFactory>()));
        // 条目由连通性探针与条件写入的状态合成：后台重试一确认，条目即时恢复
        context.Services.AddSingleton<IHealthContributor>(static sp => new S3ConnectionHealth(sp.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName), sp.GetRequiredService<S3ConditionalWriteGate>()));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 只登记命名实例的 Provider：缺省实例由别的存储提供，不读本包的配置节与连接绑定、不建客户端
    /// </remarks>
    public void ConfigureShadowed(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddMessageCatalog(ObjectStorageMessages.Catalog);
        context.Services.AddBlobStoreProvider<S3BlobStoreProvider>(context.Configuration);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!_providesDefault)
        {
            return;
        }

        // 条件写入探测与健康项首探并行：S3 连不上时两者都要等到超时（SDK 内部还会重试），串行就是两倍的启动耗时
        _conditionalWrites = services.GetRequiredService<S3ConditionalWriteGate>();
        _health = services.GetRequiredKeyedService<PeriodicHealthContributor>(HealthName);
        var probing = _conditionalWrites.StartAsync(cancellationToken).AsTask();
        await _health.StartAsync(cancellationToken).ConfigureAwait(false);
        var probed = await probing.ConfigureAwait(false);

        // 探明不认（Overwrite = false 的写入可能静默覆盖）、被拒绝或探测桶不存在（配置错误，要早暴露）即启动失败：
        // 已起的健康循环先停掉，不留给失败的特性；只有连不上是尚未确认，照常启动、后台重试，探明之前不覆盖写入由状态拒绝
        if (probed.IsFailure)
        {
            await _health.StopAsync(CancellationToken.None).ConfigureAwait(false);
            _health = null;
            probed.ThrowIfFailure();
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

        if (_conditionalWrites is not null)
        {
            await _conditionalWrites.StopAsync(cancellationToken).ConfigureAwait(false);
            _conditionalWrites = null;
        }
    }

    /// <summary>
    /// 按选项创建客户端
    /// </summary>
    /// <remarks>
    /// 有端点时指向兼容服务并只在服务端要求时计算 / 校验校验和（多数兼容服务不支持分块尾部校验和），没有端点时按区域访问 Amazon S3；没有密钥时走 SDK 默认凭据链
    /// 缺省实例用它，端点是运维配的，不过出站地址策略（ADR-0117）
    /// </remarks>
    public static AmazonS3Client CreateClient(S3ObjectStorageOptions options) => CreateClient(options, null);

    /// <summary>
    /// 按选项创建客户端，HTTP 客户端由 <paramref name="httpClients"/> 建出
    /// </summary>
    /// <remarks>
    /// 空时用 SDK 自己的工厂
    /// </remarks>
    internal static AmazonS3Client CreateClient(S3ObjectStorageOptions options, HttpClientFactory? httpClients)
    {
        ArgumentNullException.ThrowIfNull(options);
        var config = new AmazonS3Config { ForcePathStyle = options.ForcePathStyle, HttpClientFactory = httpClients };
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
    /// 连通性探针
    /// </summary>
    /// <remarks>
    /// 配了健康桶就 HEAD 它（桶不存在、无权限或服务不可达都抛出）；没配就列桶，是最轻的通用管理调用。异常由探针宿主记成 Degraded 并带异常消息
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

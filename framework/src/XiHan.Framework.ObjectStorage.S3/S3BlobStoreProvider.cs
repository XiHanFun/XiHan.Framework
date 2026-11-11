// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 命名存储实例的 S3 Provider（名 <c>s3</c>）
/// </summary>
/// <remarks>
/// 设置的键同 <see cref="S3ObjectStorageOptions"/>（不含探测间隔），每个实例按自己的端点、区域与密钥建客户端。实例必须给出 <c>AccessKey</c> 与 <c>SecretKey</c>：设置可以由租户填写（ADR-0117），没给密钥就用宿主的 AWS 凭据链，等于把宿主的身份借给了填设置的人；只给一项总是拒绝，两项都不给只在运营方打开 <see cref="AmbientCredentialsKey"/> 时允许（ADR-0178）。实例被替换、移除或注册表停止时停止重试并释放客户端。定义可能来自应用的库（RFC-0012），客户端过出站地址策略，自建存储的地址要放行（ADR-0117）。<c>ConditionalWrites</c> 开启（缺省）时建实例时在实例的 <c>HealthBucket</c> 里探一次服务是否认条件写入（含分片上传的条件完成，ADR-0089）：探明不认、被拒绝或探测桶不存在都不建，抛出携带 <c>OBJECTSTORAGE_UNAVAILABLE_003</c>、<c>OBJECTSTORAGE_DENIED_002</c> 或 <c>OBJECTSTORAGE_NOTFOUND_001</c> 的 <see cref="FrameworkException"/>，注册表转成同码的失败结果、不登记实例（ADR-0093）；只有连不上照常建出实例，在后台按退避重试，探明之前不覆盖写入返回 <c>OBJECTSTORAGE_UNAVAILABLE_004</c>（ADR-0090）；关闭时建实例不发请求。健康探测按实例的 <c>HealthBucket</c> HEAD 桶（没配时列桶），连通之后再看条件写入是否已确认。实例同时是 <c>IBlobMultipartStore</c> 与 <c>IBlobUrlSigner</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3BlobStoreProvider : IBlobStoreProvider
{
    /// <summary>
    /// Provider 名
    /// </summary>
    public const string ProviderName = "s3";

    /// <summary>
    /// 允许命名实例不给密钥、使用宿主 AWS 凭据链的配置键
    /// </summary>
    /// <remarks>
    /// 运营方的开关，缺省 <see langword="false"/>；实例自己的设置里不能打开它（ADR-0178）
    /// </remarks>
    public const string AmbientCredentialsKey = S3ObjectStorageOptions.ConfigurationSection + ":NamedInstancesUseAmbientCredentials";

    /// <summary>
    /// 命名实例不给密钥时是否用宿主的 AWS 凭据链
    /// </summary>
    private readonly bool _ambientCredentials;

    /// <summary>
    /// 时钟
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 条件写入状态的日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 按选项建客户端
    /// </summary>
    /// <remarks>
    /// 缺省建出过出站地址策略的客户端；测试换成替身
    /// </remarks>
    private readonly Func<S3ObjectStorageOptions, IAmazonS3> _clients;

    /// <summary>
    /// 日志工厂
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="clock">时钟（单次探测的超时、后台重试的退避都按它计时）</param>
    /// <param name="loggerFactory">日志工厂（条件写入的状态变化记日志）</param>
    /// <param name="addresses">出站地址策略：实例的端点可能来自应用的库，建出的客户端都过它（ADR-0117）</param>
    public S3BlobStoreProvider(TimeProvider clock, ILoggerFactory loggerFactory, OutboundAddressPolicy addresses)
        : this(clock, loggerFactory, GuardedClients(addresses), ambientCredentials: false)
    {
    }

    /// <summary>
    /// 创建，按配置决定命名实例能否不给密钥
    /// </summary>
    /// <param name="clock">时钟</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="addresses">出站地址策略</param>
    /// <param name="configuration">读 <see cref="AmbientCredentialsKey"/>；容器按这个构造建 Provider</param>
    /// <exception cref="ArgumentException"><see cref="AmbientCredentialsKey"/> 不是布尔值</exception>
    public S3BlobStoreProvider(TimeProvider clock, ILoggerFactory loggerFactory, OutboundAddressPolicy addresses, IConfiguration configuration)
        : this(clock, loggerFactory, GuardedClients(addresses), AmbientCredentialsFrom(configuration))
    {
    }

    /// <summary>
    /// 创建，客户端由 <paramref name="clients"/> 按选项建出
    /// </summary>
    internal S3BlobStoreProvider(TimeProvider clock, ILoggerFactory loggerFactory, Func<S3ObjectStorageOptions, IAmazonS3> clients)
        : this(clock, loggerFactory, clients, ambientCredentials: false)
    {
    }

    /// <summary>
    /// 创建，客户端由 <paramref name="clients"/> 按选项建出，并指定命名实例能否不给密钥
    /// </summary>
    internal S3BlobStoreProvider(TimeProvider clock, ILoggerFactory loggerFactory, Func<S3ObjectStorageOptions, IAmazonS3> clients, bool ambientCredentials)
    {
        _ambientCredentials = ambientCredentials;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<S3ConditionalWriteGate>();
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public async ValueTask<Result<IBlobStore>> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        S3ObjectStorageOptions options;
        try
        {
            options = S3ObjectStorageOptions.FromSettings(definition.Settings);
            S3ConditionalWriteProbe.RequireBucket(options, string.Empty);
            RequireCredentials(options, _ambientCredentials);
        }
        catch (ArgumentException exception)
        {
            return BlobStoreFailures.DefinitionInvalid(definition, exception.Message);
        }

        var client = _clients(options);
        var conditionalWrites = S3ConditionalWriteGate.Create(client, options, multipart: true, _clock, _logger, definition.Name);
        try
        {
            // 每个实例各探一次：命名实例可能指向不同的服务。探明不认、被拒绝或缺桶都不建——不覆盖写入不会落到不认条件写入的服务上，
            // 配置错误在登记实例时就报给调用方；只有暂时连不上照常建出，后台重试，探明之前不覆盖写入由状态拒绝
            var started = await conditionalWrites.StartAsync(cancellationToken).ConfigureAwait(false);
            if (started.IsFailure)
            {
                await conditionalWrites.DisposeAsync().ConfigureAwait(false);
                client.Dispose();
                return started.Error!;
            }

            return new S3BlobStore(client, options, _clock, _loggerFactory, conditionalWrites, owned: true);
        }
        catch
        {
            // 取消或实现缺陷：照常传播，先释放已建的客户端
            await conditionalWrites.DisposeAsync().ConfigureAwait(false);
            client.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken)
        => (store as S3BlobStore ?? throw new ArgumentException("The store was not created by the S3 provider.", nameof(store))).ProbeAsync(cancellationToken);

    /// <summary>
    /// 核对命名实例的密钥
    /// </summary>
    /// <remarks>
    /// 设置可以由租户填写：没给密钥时 SDK 走默认凭据链（环境变量、配置文件、实例角色），等于以宿主的身份访问任何它能访问的桶（ADR-0178）
    /// </remarks>
    /// <exception cref="ArgumentException">只给了一项密钥，或两项都没给而运营方没有打开环境凭据</exception>
    private static void RequireCredentials(S3ObjectStorageOptions options, bool ambientCredentials)
    {
        if (options.AccessKey is not null && options.SecretKey is not null)
        {
            return;
        }

        if (options.AccessKey is not null || options.SecretKey is not null)
        {
            throw new ArgumentException("Named S3 instances must set both 'AccessKey' and 'SecretKey'; only one of them was given.");
        }

        if (!ambientCredentials)
        {
            throw new ArgumentException($"Named S3 instances must set 'AccessKey' and 'SecretKey'. Using the host's ambient AWS credentials for named instances requires the operator to set '{AmbientCredentialsKey}' to true.");
        }
    }

    /// <summary>
    /// 读运营方的环境凭据开关
    /// </summary>
    private static bool AmbientCredentialsFrom(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration[AmbientCredentialsKey] switch
        {
            null => false,
            var text when bool.TryParse(text, out var value) => value,
            var text => throw new ArgumentException($"{AmbientCredentialsKey} must be 'true' or 'false', but was '{text}'."),
        };
    }

    /// <summary>
    /// 按选项建出过出站地址策略的客户端
    /// </summary>
    private static Func<S3ObjectStorageOptions, IAmazonS3> GuardedClients(OutboundAddressPolicy addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        return options => S3ObjectStorageFeature.CreateClient(options, new S3OutboundHttpClientFactory(addresses));
    }
}

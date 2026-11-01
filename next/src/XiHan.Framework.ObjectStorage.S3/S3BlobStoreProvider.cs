// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.S3;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 命名存储实例的 S3 Provider（名 <c>s3</c>）。
/// </summary>
/// <remarks>
/// 设置的键同 <see cref="S3ObjectStorageOptions"/>（不含探测间隔），每个实例按自己的端点、区域与密钥建客户端，实例被替换、移除或注册表停止时停止重试并释放客户端。<c>ConditionalWrites</c> 开启（缺省）时建实例时在实例的 <c>HealthBucket</c> 里探一次服务是否认条件写入（含分片上传的条件完成，ADR-0089）：只有探明不认才不建，抛出携带 <c>OBJECTSTORAGE_UNAVAILABLE_003</c> 的 <see cref="FrameworkException"/>，注册表转成同码的失败结果、不登记实例；连不上、被拒绝或探测桶不存在照常建出实例，在后台按退避重试，探明之前不覆盖写入返回 <c>OBJECTSTORAGE_UNAVAILABLE_004</c>（ADR-0090）；关闭时建实例不发请求。健康探测按实例的 <c>HealthBucket</c> HEAD 桶（没配时列桶），连通之后再看条件写入是否已确认。实例同时是 <c>IBlobMultipartStore</c> 与 <c>IBlobUrlSigner</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3BlobStoreProvider : IBlobStoreProvider
{
    /// <summary>
    /// Provider 名。
    /// </summary>
    public const string ProviderName = "s3";

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 条件写入状态的日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 按选项建客户端。
    /// </summary>
    /// <remarks>
    /// 缺省是 <see cref="S3ObjectStorageFeature.CreateClient"/>；测试换成替身。
    /// </remarks>
    private readonly Func<S3ObjectStorageOptions, IAmazonS3> _clients;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="clock">时钟（单次探测的超时、后台重试的退避都按它计时）。</param>
    /// <param name="loggerFactory">日志工厂（条件写入的状态变化记日志）。</param>
    public S3BlobStoreProvider(TimeProvider clock, ILoggerFactory loggerFactory)
        : this(clock, loggerFactory, S3ObjectStorageFeature.CreateClient)
    {
    }

    /// <summary>
    /// 创建，客户端由 <paramref name="clients"/> 按选项建出。
    /// </summary>
    internal S3BlobStoreProvider(TimeProvider clock, ILoggerFactory loggerFactory, Func<S3ObjectStorageOptions, IAmazonS3> clients)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<S3ConditionalWriteGate>();
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public async ValueTask<IBlobStore> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var options = S3ObjectStorageOptions.FromSettings(definition.Settings);
        S3ConditionalWriteProbe.RequireBucket(options, string.Empty);
        var client = _clients(options);
        var conditionalWrites = S3ConditionalWriteGate.Create(client, options, multipart: true, _clock, _logger, definition.Name);
        try
        {
            // 每个实例各探一次：命名实例可能指向不同的服务。只有探明不认才不建——不覆盖写入不会落到不认条件写入的服务上；
            // 暂时连不上、被拒绝或缺桶照常建出，后台重试，探明之前不覆盖写入由状态拒绝
            (await conditionalWrites.StartAsync(cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            return new S3BlobStore(client, options, _clock, conditionalWrites, owned: true);
        }
        catch
        {
            await conditionalWrites.DisposeAsync().ConfigureAwait(false);
            client.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken)
        => (store as S3BlobStore ?? throw new ArgumentException("The store was not created by the S3 provider.", nameof(store))).ProbeAsync(cancellationToken);
}

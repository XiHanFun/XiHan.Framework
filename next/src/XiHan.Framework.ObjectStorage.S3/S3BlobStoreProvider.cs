// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 命名存储实例的 S3 Provider（名 <c>s3</c>）。
/// </summary>
/// <remarks>
/// 设置的键同 <see cref="S3ObjectStorageOptions"/>（不含探测间隔），每个实例按自己的端点、区域与密钥建客户端，实例被替换、移除或注册表停止时释放客户端。<c>ConditionalWrites</c> 开启（缺省）时建实例先在实例的 <c>HealthBucket</c> 里探测服务是否认条件写入（含分片上传的条件完成，ADR-0089），失败抛出携带错误码的 <see cref="FrameworkException"/>，注册表转成同码的失败结果、不登记实例；关闭时建实例不发请求。健康探测按实例的 <c>HealthBucket</c> HEAD 桶，没配时列桶。实例同时是 <c>IBlobMultipartStore</c> 与 <c>IBlobUrlSigner</c>。
/// </remarks>
/// <param name="clock">时钟。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3BlobStoreProvider(TimeProvider clock) : IBlobStoreProvider
{
    /// <summary>
    /// Provider 名。
    /// </summary>
    public const string ProviderName = "s3";

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public async ValueTask<IBlobStore> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var options = S3ObjectStorageOptions.FromSettings(definition.Settings);
        S3ConditionalWriteProbe.RequireBucket(options, string.Empty);
        var client = S3ObjectStorageFeature.CreateClient(options);
        try
        {
            // 每个实例各探一次：命名实例可能指向不同的服务；探不通过就不建实例，Overwrite = false 的写入不会落到不认条件写入的服务上
            if (options.ConditionalWrites)
            {
                (await S3ConditionalWriteProbe.RunAsync(client, options, multipart: true, _clock, cancellationToken).ConfigureAwait(false)).ThrowIfFailure();
            }

            return new S3BlobStore(client, options, _clock, ownsClient: true);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken)
        => (store as S3BlobStore ?? throw new ArgumentException("The store was not created by the S3 provider.", nameof(store))).ProbeAsync(cancellationToken);
}

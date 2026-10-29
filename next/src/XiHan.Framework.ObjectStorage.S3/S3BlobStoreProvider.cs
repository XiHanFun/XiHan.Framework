// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 命名存储实例的 S3 Provider（名 <c>s3</c>）：设置的键同 <see cref="S3ObjectStorageOptions"/>（不含探测间隔），每个实例按自己的端点、区域与密钥建客户端，实例被替换、移除或注册表停止时释放客户端。
/// 建实例不发请求（凭据或端点错了在首次调用与探测时才暴露）；探测按实例的 <c>HealthBucket</c> HEAD 桶，没配时列桶。实例同时是 <c>IBlobMultipartStore</c> 与 <c>IBlobUrlSigner</c>。
/// </summary>
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
    public ValueTask<IBlobStore> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var options = S3ObjectStorageOptions.FromSettings(definition.Settings);
        return ValueTask.FromResult<IBlobStore>(new S3BlobStore(S3ObjectStorageFeature.CreateClient(options), options, _clock, ownsClient: true));
    }

    /// <inheritdoc />
    public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken)
        => (store as S3BlobStore ?? throw new ArgumentException("The store was not created by the S3 provider.", nameof(store))).ProbeAsync(cancellationToken);
}

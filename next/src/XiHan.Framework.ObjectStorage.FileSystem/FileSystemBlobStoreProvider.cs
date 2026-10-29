// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 命名存储实例的文件系统 Provider（名 <c>filesystem</c>）：设置的键同 <see cref="FileSystemObjectStorageOptions"/>，<c>RootPath</c> 必填（各实例各用各的目录，与缺省实例的目录也不要重叠）。
/// 建出时即建根目录并扫描已用量；探测看总配额是否还有余量，与缺省实例的健康项同一口径。预签名地址由宿主代收发（<c>HostedBlobUrlSigner.ForStore</c>）。
/// </summary>
/// <param name="clock">时钟。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class FileSystemBlobStoreProvider(TimeProvider clock) : IBlobStoreProvider
{
    /// <summary>
    /// Provider 名。
    /// </summary>
    public const string ProviderName = "filesystem";

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
        var store = new FileSystemBlobStore(FileSystemObjectStorageOptions.FromSettings(definition.Settings), _clock);
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return store;
    }

    /// <inheritdoc />
    public ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken)
    {
        var fileSystem = store as FileSystemBlobStore ?? throw new ArgumentException("The store was not created by the file system provider.", nameof(store));
        var status = fileSystem.Check()[0];
        return ValueTask.FromResult(status.State == LifecycleState.Ready ? Result.Success() : Result.Failure(status.Error!));
    }
}

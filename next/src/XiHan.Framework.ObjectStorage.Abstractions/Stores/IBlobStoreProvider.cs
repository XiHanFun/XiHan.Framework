// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 按定义建存储实例的 Provider（每个存储包一个，按单例登记）。
/// </summary>
/// <remarks>
/// <see cref="BlobStores"/> 据 <see cref="BlobStoreDefinition.Provider"/> 选中它。建出的实例实现了 <c>IBlobMultipartStore</c> / <c>IBlobUrlSigner</c> 时同样可用；实例持有需要释放的资源（如 S3 客户端）时实现 <see cref="IDisposable"/> 或 <see cref="IAsyncDisposable"/>，由注册表在移除、替换与停止时释放。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IBlobStoreProvider
{
    /// <summary>
    /// Provider 名（如 <c>filesystem</c>、<c>s3</c>），不区分大小写。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 按定义建实例。
    /// </summary>
    /// <remarks>
    /// 设置不合法（含未知的设置键）时抛出 <see cref="ArgumentException"/>，后端此刻建不出来时抛出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>（注册表分别翻译为 <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/> 与 <see cref="ObjectStorageErrors.StoreUnavailable"/>）。
    /// </remarks>
    ValueTask<IBlobStore> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// 探测实例是否可用（健康项用）。
    /// </summary>
    /// <remarks>
    /// 可用返回成功，不可用返回失败（通常是 <see cref="ObjectStorageErrors.StoreUnavailable"/>）。
    /// </remarks>
    ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken);
}

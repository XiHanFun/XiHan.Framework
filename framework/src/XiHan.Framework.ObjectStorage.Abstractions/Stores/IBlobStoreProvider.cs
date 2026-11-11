// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 按定义建存储实例的 Provider（每个存储包一个，按单例登记）
/// </summary>
/// <remarks>
/// <see cref="BlobStores"/> 据 <see cref="BlobStoreDefinition.Provider"/> 选中它。建出的实例实现了 <c>IBlobMultipartStore</c> / <c>IBlobUrlSigner</c> 时同样可用；实例持有需要释放的资源（如 S3 客户端）时实现 <see cref="IDisposable"/> 或 <see cref="IAsyncDisposable"/>，由注册表在移除、替换与停止时释放
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IBlobStoreProvider
{
    /// <summary>
    /// Provider 名（如 <c>filesystem</c>、<c>s3</c>），不区分大小写
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 按定义建实例
    /// </summary>
    /// <remarks>
    /// 失败以结果返回，不抛出（ADR-0150）：
    /// <list type="bullet">
    /// <item>设置不合法（含未知的设置键）→ <see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>（<see cref="BlobStoreFailures.DefinitionInvalid"/>）</item>
    /// <item>后端此刻建不出来（目录无权限、磁盘错误）→ <see cref="ObjectStorageErrors.StoreUnavailable"/>（<see cref="BlobStoreFailures.Unavailable"/>）</item>
    /// <item>建实例时的探测发现实例不能用（如 S3 探明服务不认条件写入、探测被拒绝或探测桶不存在）→ 探测给出的错误码；只是暂时连不上的照常建出</item>
    /// </list>
    /// 失败时实现自己释放已建的资源。只有 <paramref name="definition"/> 为 <see langword="null"/> 这类编程错误抛出
    /// </remarks>
    ValueTask<Result<IBlobStore>> CreateAsync(BlobStoreDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// 探测实例是否可用（健康项用）
    /// </summary>
    /// <remarks>
    /// 可用返回成功，不可用返回失败（通常是 <see cref="ObjectStorageErrors.StoreUnavailable"/>）
    /// </remarks>
    ValueTask<Result> ProbeAsync(IBlobStore store, CancellationToken cancellationToken);
}

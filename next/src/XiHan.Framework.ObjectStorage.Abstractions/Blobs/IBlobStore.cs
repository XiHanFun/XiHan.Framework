// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 对象存储（RFC-0012）。
/// </summary>
/// <remarks>
/// 容器里按键存放字节与元数据。写入与删除是批量的（按顺序逐项执行，返回逐项结果，两项之间检查取消，已完成的不回滚）；单条便捷方法见 <see cref="BlobStoreExtensions"/>。标识不合法在构造时抛出；基础设施失败返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IBlobStore
{
    /// <summary>
    /// 批量写入。
    /// </summary>
    /// <remarks>
    /// 已存在且 <see cref="BlobPutOptions.Overwrite"/> 为 <see langword="false"/> 的项返回 <see cref="ObjectStorageErrors.BlobExists"/>；超过配额的项返回 <see cref="ObjectStorageErrors.QuotaExceeded"/>。
    /// </remarks>
    ValueTask<IReadOnlyList<BlobPutResult>> PutAsync(ContainerName container, IReadOnlyList<BlobUpload> uploads, CancellationToken cancellationToken);

    /// <summary>
    /// 读取内容与元数据。
    /// </summary>
    /// <remarks>
    /// 不存在返回 <see cref="ObjectStorageErrors.BlobNotFound"/>。
    /// </remarks>
    ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 读取一段字节与元数据（音视频拖动、断点续传下载）。
    /// </summary>
    /// <remarks>
    /// <see cref="BlobContent.Range"/> 给出实际范围，<see cref="BlobMetadata.Length"/> 仍是对象总长。不存在返回 <see cref="ObjectStorageErrors.BlobNotFound"/>；起点不在对象内（含空对象）返回 <see cref="ObjectStorageErrors.RangeNotSatisfiable"/>。
    /// </remarks>
    ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, BlobRange range, CancellationToken cancellationToken);

    /// <summary>
    /// 只读元数据。
    /// </summary>
    /// <remarks>
    /// 不存在返回 <see cref="ObjectStorageErrors.BlobNotFound"/>。
    /// </remarks>
    ValueTask<Result<BlobMetadata>> HeadAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 批量删除。
    /// </summary>
    /// <remarks>
    /// 不存在的项成功且报告为不存在。
    /// </remarks>
    ValueTask<IReadOnlyList<BlobDeleteResult>> DeleteAsync(ContainerName container, IReadOnlyList<BlobKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 按前缀分页列举，键的序数顺序。
    /// </summary>
    ValueTask<Result<BlobPage>> ListAsync(ContainerName container, BlobQuery query, CancellationToken cancellationToken);
}

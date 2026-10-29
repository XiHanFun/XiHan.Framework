// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Multipart;

/// <summary>
/// 分片上传（能力 <c>objectstorage.multipart@1</c>，RFC-0012）：大文件分段上传、断点续传。发起得到上传标识；各分片（编号 1 到 <see cref="BlobMultipart.MaxPartNumber"/>，可乱序、可重传，后一次覆盖前一次）独立上传；
/// 完成时按编号升序给出要拼接的分片与其 ETag，除最后一片外每片至少 <see cref="BlobMultipart.MinPartBytes"/>，对象原子出现；放弃即丢弃已上传的分片。完成前对象不可见，分片不计入读取与列举。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IBlobMultipartStore
{
    /// <summary>
    /// 发起分片上传；内容类型、自定义元数据与是否覆盖取自 <paramref name="options"/>。不覆盖且对象已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>。
    /// </summary>
    ValueTask<Result<BlobMultipartUpload>> StartAsync(ContainerName container, BlobKey key, BlobPutOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 上传一个分片（流从当前位置读到末尾）；同一编号再传即覆盖。上传不存在或已结束返回 <see cref="ObjectStorageErrors.UploadNotFound"/>。
    /// </summary>
    ValueTask<Result<BlobPart>> UploadPartAsync(BlobMultipartUpload upload, int partNumber, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// 已上传的分片，按编号升序（断点续传时据此跳过已传的分片）。上传不存在或已结束返回 <see cref="ObjectStorageErrors.UploadNotFound"/>。
    /// </summary>
    ValueTask<Result<IReadOnlyList<BlobPart>>> ListPartsAsync(BlobMultipartUpload upload, CancellationToken cancellationToken);

    /// <summary>
    /// 按 <paramref name="parts"/>（编号严格升序、ETag 与已上传的一致）拼成对象并结束上传。
    /// 分片清单不合法（为空、乱序、未上传或 ETag 不符、非末片过小）返回 <see cref="ObjectStorageErrors.PartsInvalid"/>，上传保留可再完成；不覆盖且对象已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>。
    /// </summary>
    ValueTask<Result<BlobMetadata>> CompleteAsync(BlobMultipartUpload upload, IReadOnlyList<BlobPart> parts, CancellationToken cancellationToken);

    /// <summary>
    /// 放弃上传并丢弃已上传的分片；上传不存在也成功（幂等）。
    /// </summary>
    ValueTask<Result> AbortAsync(BlobMultipartUpload upload, CancellationToken cancellationToken);
}

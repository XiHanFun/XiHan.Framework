// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Multipart;

/// <summary>
/// 分片上传（能力 <c>objectstorage.multipart@1</c>，RFC-0012）
/// </summary>
/// <remarks>
/// 大文件分段上传、断点续传。发起得到上传标识；各分片（编号 1 到 <see cref="BlobMultipart.MaxPartNumber"/>，可乱序、可重传，后一次覆盖前一次）独立上传；完成时按编号升序给出要拼接的分片与其 ETag，除最后一片外每片至少 <see cref="BlobMultipart.MinPartBytes"/>，对象原子出现；放弃即丢弃已上传的分片，放弃是批量的（清理一批过期或用户取消的上传一次调用），逐项返回结果。完成前对象不可见，分片不计入读取与列举
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IBlobMultipartStore
{
    /// <summary>
    /// 发起分片上传
    /// </summary>
    /// <remarks>
    /// 内容类型、自定义元数据与是否覆盖取自 <paramref name="options"/>。不覆盖且对象已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>
    /// </remarks>
    ValueTask<Result<BlobMultipartUpload>> StartAsync(ContainerName container, BlobKey key, BlobPutOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 上传一个分片（流从当前位置读到末尾）
    /// </summary>
    /// <remarks>
    /// 同一编号再传即覆盖。上传不存在或已结束返回 <see cref="ObjectStorageErrors.UploadNotFound"/>
    /// </remarks>
    ValueTask<Result<BlobPart>> UploadPartAsync(BlobMultipartUpload upload, int partNumber, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// 已上传的分片，按编号升序（断点续传时据此跳过已传的分片）
    /// </summary>
    /// <remarks>
    /// 上传不存在或已结束返回 <see cref="ObjectStorageErrors.UploadNotFound"/>
    /// </remarks>
    ValueTask<Result<IReadOnlyList<BlobPart>>> ListPartsAsync(BlobMultipartUpload upload, CancellationToken cancellationToken);

    /// <summary>
    /// 按 <paramref name="parts"/>（编号严格升序、ETag 与已上传的一致）拼成对象并结束上传
    /// </summary>
    /// <remarks>
    /// 分片清单不合法（为空、乱序、未上传或 ETag 不符、非末片过小）返回 <see cref="ObjectStorageErrors.PartsInvalid"/>，上传保留可再完成；不覆盖且对象已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>
    /// </remarks>
    ValueTask<Result<BlobMetadata>> CompleteAsync(BlobMultipartUpload upload, IReadOnlyList<BlobPart> parts, CancellationToken cancellationToken);

    /// <summary>
    /// 批量放弃上传并丢弃已上传的分片
    /// </summary>
    /// <remarks>
    /// <para>按入参顺序逐项执行，返回与入参等长同序的逐项结果。上传不存在或已结束也成功（幂等），所以同一批里重复的上传每一项都成功；空集合不访问存储，返回空列表。存储不可达只记在该项上（<see cref="ObjectStorageErrors.StoreUnavailable"/>），其余项照常执行</para>
    /// <para>取消：每一项之前检查取消，已取消即抛出 <see cref="OperationCanceledException"/>；此前已放弃的不恢复，放弃是幂等的，整批重试即可。集合里有 <see langword="null"/> 是编程错误，在放弃任何上传之前抛出。单条便捷方法见 <see cref="BlobMultipartStoreExtensions"/></para>
    /// </remarks>
    /// <param name="uploads">要放弃的上传，可以分属不同的容器与键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentException">集合里有 <see langword="null"/></exception>
    ValueTask<IReadOnlyList<Result>> AbortAsync(IReadOnlyList<BlobMultipartUpload> uploads, CancellationToken cancellationToken);
}

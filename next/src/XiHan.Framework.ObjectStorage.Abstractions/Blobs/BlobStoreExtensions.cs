// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 单条便捷方法：都转调批量方法。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class BlobStoreExtensions
{
    /// <summary>
    /// 写入一个流。
    /// </summary>
    public static async ValueTask<Result<BlobMetadata>> PutAsync(this IBlobStore store, ContainerName container, BlobKey key, Stream content, BlobPutOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var results = await store.PutAsync(container, [new BlobUpload(key, content, options)], cancellationToken).ConfigureAwait(false);
        return results[0].Outcome;
    }

    /// <summary>
    /// 写入一段字节。
    /// </summary>
    public static async ValueTask<Result<BlobMetadata>> PutAsync(this IBlobStore store, ContainerName container, BlobKey key, ReadOnlyMemory<byte> content, BlobPutOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        return await store.PutAsync(container, key, stream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除一个对象；返回是否存在过。
    /// </summary>
    public static async ValueTask<Result<bool>> DeleteAsync(this IBlobStore store, ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var results = await store.DeleteAsync(container, [key], cancellationToken).ConfigureAwait(false);
        return results[0].Outcome;
    }

    /// <summary>
    /// 对象是否存在；不存在是成功的 <see langword="false"/>，其他失败原样返回。
    /// </summary>
    public static async ValueTask<Result<bool>> ExistsAsync(this IBlobStore store, ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var head = await store.HeadAsync(container, key, cancellationToken).ConfigureAwait(false);
        if (head.IsSuccess)
        {
            return Result.Success(true);
        }

        return string.Equals(head.Error!.Code, ObjectStorageErrors.BlobNotFound, StringComparison.Ordinal) ? Result.Success(false) : head.Error;
    }

    /// <summary>
    /// 读出全部字节。
    /// </summary>
    public static async ValueTask<Result<byte[]>> ReadAllBytesAsync(this IBlobStore store, ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        var got = await store.GetAsync(container, key, cancellationToken).ConfigureAwait(false);
        if (!got.TryGetValue(out var blob))
        {
            return got.Error!;
        }

        await using (blob)
        {
            using var buffer = new MemoryStream();
            await blob.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return Result.Success(buffer.ToArray());
        }
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储：容器即桶，键即对象键。不覆盖写入用条件请求（<c>If-None-Match: *</c>）保证原子，已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>；
/// 批量删除先逐键 HEAD 得知是否存在，再一次 <c>DeleteObjects</c>；列举经 <c>ListObjectsV2</c>，条目只有键、长度、ETag 与修改时间（内容类型为默认值、自定义元数据为空，需要时再 HEAD）。
/// 不存在的键与桶映射为 <see cref="ObjectStorageErrors.BlobNotFound"/>，其余服务端与网络错误映射为 <see cref="ObjectStorageErrors.StoreUnavailable"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3BlobStore : IBlobStore
{
    /// <summary>
    /// 自定义元数据在 S3 头里的前缀。
    /// </summary>
    private const string MetadataPrefix = "x-amz-meta-";

    /// <summary>
    /// 一次 DeleteObjects 允许的最大键数。
    /// </summary>
    private const int DeleteBatchSize = 1000;

    /// <summary>
    /// S3 客户端。
    /// </summary>
    private readonly IAmazonS3 _client;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly S3ObjectStorageOptions _options;

    /// <summary>
    /// 时钟；写入结果的修改时间来自它（PutObject 的响应不带修改时间）。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建。
    /// </summary>
    public S3BlobStore(IAmazonS3 client, S3ObjectStorageOptions options, TimeProvider clock)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<BlobPutResult>> PutAsync(ContainerName container, IReadOnlyList<BlobUpload> uploads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        var results = new List<BlobPutResult>(uploads.Count);
        foreach (var upload in uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new BlobPutResult(upload.Key, await PutOneAsync(container, upload, cancellationToken).ConfigureAwait(false)));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        GetObjectResponse? response = null;
        try
        {
            response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = container.Value, Key = key.Value }, cancellationToken).ConfigureAwait(false);
            var metadata = new BlobMetadata(key, response.ContentLength, response.Headers.ContentType ?? BlobPutOptions.DefaultContentType, Unquote(response.ETag), ToTimestamp(response.LastModified), ReadMetadata(response.Metadata));
            var content = new BlobContent(metadata, new S3ObjectStream(response));
            response = null;
            return Result.Success(content);
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return NotFound(container, key);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
        finally
        {
            // 没能交出去的响应要释放（它持有 HTTP 连接）
            response?.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobMetadata>> HeadAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = container.Value, Key = key.Value }, cancellationToken).ConfigureAwait(false);
            return Result.Success(new BlobMetadata(key, response.ContentLength, response.Headers.ContentType ?? BlobPutOptions.DefaultContentType, Unquote(response.ETag), ToTimestamp(response.LastModified), ReadMetadata(response.Metadata)));
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return NotFound(container, key);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<BlobDeleteResult>> DeleteAsync(ContainerName container, IReadOnlyList<BlobKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return [];
        }

        // S3 的删除不报告对象是否存在过：先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键一次批量删除
        var outcomes = new Result<bool>[keys.Count];
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var toDelete = new List<string>();
        for (var index = 0; index < keys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var head = await HeadAsync(container, keys[index], cancellationToken).ConfigureAwait(false);
            if (head.IsSuccess)
            {
                if (claimed.Add(keys[index].Value))
                {
                    toDelete.Add(keys[index].Value);
                    outcomes[index] = Result.Success(true);
                }
                else
                {
                    outcomes[index] = Result.Success(false);
                }
            }
            else
            {
                outcomes[index] = head.Error!.Code == ObjectStorageErrors.BlobNotFound ? Result.Success(false) : Result.Failure<bool>(head.Error);
            }
        }

        var failed = new Dictionary<string, Error>(StringComparer.Ordinal);
        foreach (var batch in toDelete.Chunk(DeleteBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await _client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = container.Value,
                    Objects = [.. batch.Select(static key => new KeyVersion { Key = key })],
                    Quiet = true,
                }, cancellationToken).ConfigureAwait(false);
                foreach (var error in response.DeleteErrors ?? [])
                {
                    failed[error.Key] = Unavailable(error.Code + ": " + error.Message);
                }
            }
            catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
            {
                var error = Unavailable(exception);
                foreach (var key in batch)
                {
                    failed[key] = error;
                }
            }
        }

        var results = new List<BlobDeleteResult>(keys.Count);
        for (var index = 0; index < keys.Count; index++)
        {
            // 只有"存在且本次负责删除"的项会因删除失败而失败
            var outcome = outcomes[index];
            if (outcome.IsSuccess && outcome.ValueOrThrow() && failed.TryGetValue(keys[index].Value, out var error))
            {
                outcome = Result.Failure<bool>(error);
            }

            results.Add(new BlobDeleteResult(keys[index], outcome));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobPage>> ListAsync(ContainerName container, BlobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        try
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = container.Value,
                Prefix = query.Prefix,
                MaxKeys = query.PageSize,
                ContinuationToken = query.ContinuationToken,
            }, cancellationToken).ConfigureAwait(false);

            var items = new List<BlobMetadata>(response.KeyCount ?? 0);
            foreach (var entry in response.S3Objects ?? [])
            {
                // 不符合键文法的对象（如以 / 结尾的"目录"占位）不是本契约的对象，跳过
                if (BlobKey.IsValid(entry.Key))
                {
                    items.Add(new BlobMetadata(new BlobKey(entry.Key), entry.Size ?? 0, BlobPutOptions.DefaultContentType, Unquote(entry.ETag), ToTimestamp(entry.LastModified), EmptyMetadata));
                }
            }

            return Result.Success(new BlobPage(items, response.IsTruncated == true ? response.NextContinuationToken : null));
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            // 桶不存在视作空容器，与文件系统实现里容器目录不存在的语义一致
            return Result.Success(new BlobPage([], null));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 空的自定义元数据。
    /// </summary>
    private static IReadOnlyDictionary<string, string> EmptyMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 写一个对象：不覆盖时先 HEAD 再带条件写入（服务端支持条件写入时由它保证原子）；桶不存在且开启 <see cref="S3ObjectStorageOptions.EnsureBuckets"/> 时建桶后重试一次。
    /// </summary>
    private async ValueTask<Result<BlobMetadata>> PutOneAsync(ContainerName container, BlobUpload upload, CancellationToken cancellationToken)
    {
        if (!upload.Options.Overwrite)
        {
            var existing = await HeadAsync(container, upload.Key, cancellationToken).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                return Exists(container, upload.Key);
            }

            if (existing.Error!.Code != ObjectStorageErrors.BlobNotFound)
            {
                return Result.Failure<BlobMetadata>(existing.Error);
            }
        }

        // PutObject 需要内容长度：不可定位的流先落到临时文件（关闭即删），不把整个对象装进内存
        Stream content = upload.Content;
        FileStream? buffered = null;
        try
        {
            if (!content.CanSeek)
            {
                buffered = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await content.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
                buffered.Position = 0;
                content = buffered;
            }

            var length = content.Length - content.Position;
            var ensured = false;
            while (true)
            {
                try
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = container.Value,
                        Key = upload.Key.Value,
                        InputStream = content,
                        AutoCloseStream = false,
                        ContentType = upload.Options.ContentType,
                    };
                    if (!upload.Options.Overwrite)
                    {
                        request.IfNoneMatch = "*";
                    }

                    foreach (var (name, value) in upload.Options.Metadata)
                    {
                        request.Metadata[name] = value;
                    }

                    var response = await _client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
                    return Result.Success(new BlobMetadata(upload.Key, length, upload.Options.ContentType, Unquote(response.ETag), _clock.GetUtcNow(), new Dictionary<string, string>(upload.Options.Metadata, StringComparer.Ordinal)));
                }
                catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    return Exists(container, upload.Key);
                }
                catch (AmazonS3Exception exception) when (!ensured && _options.EnsureBuckets && IsNoSuchBucket(exception))
                {
                    ensured = true;
                    await _client.PutBucketAsync(new PutBucketRequest { BucketName = container.Value }, cancellationToken).ConfigureAwait(false);
                    content.Position = content.Length - length;
                }
            }
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
        finally
        {
            if (buffered is not null)
            {
                await buffered.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 响应里的自定义元数据（去掉 <c>x-amz-meta-</c> 前缀）。
    /// </summary>
    private static Dictionary<string, string> ReadMetadata(MetadataCollection metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in metadata.Keys)
        {
            var name = key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase) ? key[MetadataPrefix.Length..] : key;
            result[name] = metadata[key];
        }

        return result;
    }

    /// <summary>
    /// 去掉 ETag 两端的引号。
    /// </summary>
    private static string Unquote(string? etag) => (etag ?? string.Empty).Trim('"');

    /// <summary>
    /// 把 SDK 的修改时间（UTC）转成带偏移的时刻；缺失时为最小值。
    /// </summary>
    private static DateTimeOffset ToTimestamp(DateTime? value)
        => value is { } time ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)) : DateTimeOffset.MinValue;

    /// <summary>
    /// 对象或桶不存在。
    /// </summary>
    private static bool IsNotFound(AmazonS3Exception exception)
        => exception.StatusCode == HttpStatusCode.NotFound || exception.ErrorCode is "NoSuchKey" or "NotFound" || IsNoSuchBucket(exception);

    /// <summary>
    /// 桶不存在。
    /// </summary>
    private static bool IsNoSuchBucket(AmazonS3Exception exception) => exception.ErrorCode == "NoSuchBucket";

    /// <summary>
    /// 该异常是存储故障（服务端错误、网络错误、SDK 超时）而不是调用方取消。
    /// </summary>
    private static bool IsStoreFailure(Exception exception, CancellationToken cancellationToken)
        => exception is AmazonServiceException or AmazonClientException or HttpRequestException or IOException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>
    /// 不存在。
    /// </summary>
    private static Error NotFound(ContainerName container, BlobKey key)
        => Error.NotFound(ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.BlobNotFound, MessageArguments.Of(("container", container.Value), ("key", key.Value)));

    /// <summary>
    /// 已存在且不允许覆盖。
    /// </summary>
    private static Error Exists(ContainerName container, BlobKey key)
        => Error.Conflict(ObjectStorageErrors.BlobExists, ObjectStorageMessages.BlobExists, MessageArguments.Of(("container", container.Value), ("key", key.Value)));

    /// <summary>
    /// 存储不可用。
    /// </summary>
    private static Error Unavailable(Exception exception) => Unavailable(exception.Message);

    /// <summary>
    /// 存储不可用。
    /// </summary>
    private static Error Unavailable(string reason)
        => Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", reason)));
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储。
/// </summary>
/// <remarks>
/// 容器即桶，键即对象键。不覆盖写入用条件请求（<c>If-None-Match: *</c>）保证原子，已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>；批量删除先逐键 HEAD 得知是否存在，再一次 <c>DeleteObjects</c>；列举经 <c>ListObjectsV2</c>，条目只有键、长度、ETag 与修改时间（内容类型为默认值、自定义元数据为空，需要时再 HEAD）。范围读取经 <c>Range</c> 请求头；分片上传直接映射 S3 的 multipart（不覆盖的完成带 <c>If-None-Match: *</c>）；预签名地址由 SDK 本地签出（签名 V4），客户端直连存储。不存在的键与桶映射为 <see cref="ObjectStorageErrors.BlobNotFound"/>，其余服务端与网络错误映射为 <see cref="ObjectStorageErrors.StoreUnavailable"/>。经公开构造函数创建的实例不拥有客户端，<see cref="Dispose"/> 什么都不做；命名实例（<see cref="S3BlobStoreProvider"/>）自己建客户端，释放实例即释放客户端。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class S3BlobStore : IBlobStore, IBlobMultipartStore, IBlobUrlSigner, IDisposable
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
    /// 时钟。
    /// </summary>
    /// <remarks>
    /// 写入结果的修改时间来自它（PutObject 的响应不带修改时间）。
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 是否拥有客户端（释放实例时一并释放）。
    /// </summary>
    private readonly bool _ownsClient;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// 客户端归调用方（或容器）所有。
    /// </remarks>
    public S3BlobStore(IAmazonS3 client, S3ObjectStorageOptions options, TimeProvider clock)
        : this(client, options, clock, ownsClient: false)
    {
    }

    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// <paramref name="ownsClient"/> 为真时释放实例即释放客户端。
    /// </remarks>
    internal S3BlobStore(IAmazonS3 client, S3ObjectStorageOptions options, TimeProvider clock, bool ownsClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// 释放：只在拥有客户端时释放客户端。
    /// </summary>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    /// <summary>
    /// 连通性探测：按本实例的 <see cref="S3ObjectStorageOptions.HealthBucket"/> HEAD 桶或列桶（同缺省健康项）。
    /// </summary>
    internal ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
        => S3ObjectStorageFeature.ProbeAsync(_client, _options.HealthBucket, cancellationToken);

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

    /// <inheritdoc />
    public async ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, BlobRange range, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        GetObjectResponse? response = null;
        try
        {
            var byteRange = range.Length is { } length ? new ByteRange(range.Offset, range.Offset + length - 1) : new ByteRange($"bytes={range.Offset.ToString(CultureInfo.InvariantCulture)}-");
            response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = container.Value, Key = key.Value, ByteRange = byteRange }, cancellationToken).ConfigureAwait(false);

            // 206 的 Content-Range 是 bytes 起-止/总长；服务端忽略了 Range（200 整对象）时不猜，按存储故障报告
            if (!TryParseContentRange(response.ContentRange, out var resolved))
            {
                return Unavailable("the server ignored the range request");
            }

            var metadata = new BlobMetadata(key, resolved.TotalLength, response.Headers.ContentType ?? BlobPutOptions.DefaultContentType, Unquote(response.ETag), ToTimestamp(response.LastModified), ReadMetadata(response.Metadata));
            var content = new BlobContent(metadata, new S3ObjectStream(response), resolved);
            response = null;
            return Result.Success(content);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // 起点不在对象内：再 HEAD 一次拿总长写进错误参数（对象此间被删时如实报告不存在）
            var head = await HeadAsync(container, key, cancellationToken).ConfigureAwait(false);
            return head.TryGetValue(out var found)
                ? Error.Validation(ObjectStorageErrors.RangeNotSatisfiable, ObjectStorageMessages.RangeNotSatisfiable, MessageArguments.Of(("offset", range.Offset), ("length", found.Length)))
                : Result.Failure<BlobContent>(head.Error!);
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
            response?.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobMultipartUpload>> StartAsync(ContainerName container, BlobKey key, BlobPutOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Overwrite)
        {
            var existing = await HeadAsync(container, key, cancellationToken).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                return Exists(container, key);
            }

            if (existing.Error!.Code != ObjectStorageErrors.BlobNotFound)
            {
                return Result.Failure<BlobMultipartUpload>(existing.Error);
            }
        }

        var ensured = false;
        while (true)
        {
            try
            {
                var request = new InitiateMultipartUploadRequest { BucketName = container.Value, Key = key.Value, ContentType = options.ContentType };
                foreach (var (name, value) in options.Metadata)
                {
                    request.Metadata[name] = value;
                }

                var response = await _client.InitiateMultipartUploadAsync(request, cancellationToken).ConfigureAwait(false);
                return Result.Success(new BlobMultipartUpload(container, key, response.UploadId, options.Overwrite));
            }
            catch (AmazonS3Exception exception) when (!ensured && _options.EnsureBuckets && IsNoSuchBucket(exception))
            {
                ensured = true;
                await _client.PutBucketAsync(new PutBucketRequest { BucketName = container.Value }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
            {
                return Unavailable(exception);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobPart>> UploadPartAsync(BlobMultipartUpload upload, int partNumber, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(content);
        BlobMultipart.ValidatePartNumber(partNumber);

        // UploadPart 需要分片长度：不可定位的流先落到临时文件（关闭即删）
        Stream source = content;
        FileStream? buffered = null;
        try
        {
            if (!source.CanSeek)
            {
                buffered = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await source.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
                buffered.Position = 0;
                source = buffered;
            }

            var length = source.Length - source.Position;
            var response = await _client.UploadPartAsync(new UploadPartRequest
            {
                BucketName = upload.Container.Value,
                Key = upload.Key.Value,
                UploadId = upload.UploadId,
                PartNumber = partNumber,
                InputStream = source,
                PartSize = length,
            }, cancellationToken).ConfigureAwait(false);
            return Result.Success(new BlobPart(partNumber, Unquote(response.ETag), length));
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception))
        {
            return UploadNotFound(upload);
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

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<BlobPart>>> ListPartsAsync(BlobMultipartUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        try
        {
            var parts = new List<BlobPart>();
            int? marker = null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await _client.ListPartsAsync(new ListPartsRequest
                {
                    BucketName = upload.Container.Value,
                    Key = upload.Key.Value,
                    UploadId = upload.UploadId,
                    PartNumberMarker = marker?.ToString(CultureInfo.InvariantCulture),
                }, cancellationToken).ConfigureAwait(false);
                foreach (var part in response.Parts ?? [])
                {
                    parts.Add(new BlobPart(part.PartNumber ?? 0, Unquote(part.ETag), part.Size ?? 0));
                }

                if (response.IsTruncated != true)
                {
                    break;
                }

                marker = response.NextPartNumberMarker;
            }

            return Result.Success<IReadOnlyList<BlobPart>>([.. parts.OrderBy(static p => p.PartNumber)]);
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception))
        {
            return UploadNotFound(upload);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobMetadata>> CompleteAsync(BlobMultipartUpload upload, IReadOnlyList<BlobPart> parts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsEmpty);
        }

        // 顺序在本地先查，与文件系统实现给出同一个错误；分片是否上传、ETag 与最小分片由服务端判定
        for (var index = 1; index < parts.Count; index++)
        {
            if (parts[index].PartNumber <= parts[index - 1].PartNumber)
            {
                return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsUnordered, MessageArguments.Of(("part", parts[index].PartNumber)));
            }
        }

        // 不覆盖：先 HEAD（服务端不支持条件完成时也能拦下），再带 If-None-Match 完成（支持时由它保证原子）
        if (!upload.Overwrite)
        {
            var existing = await HeadAsync(upload.Container, upload.Key, cancellationToken).ConfigureAwait(false);
            if (existing.IsSuccess)
            {
                return Exists(upload.Container, upload.Key);
            }

            if (existing.Error!.Code != ObjectStorageErrors.BlobNotFound)
            {
                return Result.Failure<BlobMetadata>(existing.Error);
            }
        }

        try
        {
            var request = new CompleteMultipartUploadRequest
            {
                BucketName = upload.Container.Value,
                Key = upload.Key.Value,
                UploadId = upload.UploadId,
                PartETags = [.. parts.Select(static p => new PartETag(p.PartNumber, p.ETag))],
            };
            if (!upload.Overwrite)
            {
                request.IfNoneMatch = "*";
            }

            await _client.CompleteMultipartUploadAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return Exists(upload.Container, upload.Key);
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception))
        {
            return UploadNotFound(upload);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode is "InvalidPart" or "InvalidPartOrder" or "EntityTooSmall" or "MalformedXML")
        {
            return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsRejected, MessageArguments.Of(("reason", exception.ErrorCode + ": " + exception.Message)));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }

        // 完成的响应只有 ETag：再 HEAD 一次拿长度、内容类型与自定义元数据
        return await HeadAsync(upload.Container, upload.Key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result> AbortAsync(BlobMultipartUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = upload.Container.Value, Key = upload.Key.Value, UploadId = upload.UploadId }, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception) || IsNotFound(exception))
        {
            return Result.Success();
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobSignedUrl>> SignAsync(ContainerName container, BlobKey key, BlobUrlRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Upload is { } upload && (upload.Container != container || upload.Key != key))
        {
            throw new ArgumentException("The multipart upload belongs to another blob.", nameof(request));
        }

        var expiresAt = _clock.GetUtcNow() + request.ExpiresIn;
        var presign = new GetPreSignedUrlRequest
        {
            BucketName = container.Value,
            Key = key.Value,
            Verb = request.Method == BlobUrlMethod.Get ? HttpVerb.GET : HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            // SDK 缺省按 HTTPS 签：兼容服务的端点是 http 时要显式指定，否则签出的地址连不上
            Protocol = _options.ServiceUrl is { } url && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS,
        };
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.ContentType is { } contentType)
        {
            presign.ContentType = contentType;
            headers["Content-Type"] = contentType;
        }

        if (request.DownloadFileName is { } fileName)
        {
            presign.ResponseHeaderOverrides.ContentDisposition = BlobContentDisposition.Attachment(fileName);
        }

        if (request.Upload is { } part)
        {
            presign.UploadId = part.UploadId;
            presign.PartNumber = request.PartNumber!.Value;
        }

        try
        {
            var signed = await _client.GetPreSignedURLAsync(presign).ConfigureAwait(false);
            return Result.Success(new BlobSignedUrl(new Uri(signed), request.Method, expiresAt, headers));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 解析 <c>Content-Range: bytes 起-止/总长</c>。
    /// </summary>
    private static bool TryParseContentRange(string? header, out BlobContentRange range)
    {
        range = null!;
        if (header is null || !header.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var slash = header.IndexOf('/', StringComparison.Ordinal);
        var dash = header.IndexOf('-', StringComparison.Ordinal);
        if (slash < 0 || dash < 0 || dash > slash
            || !long.TryParse(header.AsSpan(6, dash - 6), NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            || !long.TryParse(header.AsSpan(dash + 1, slash - dash - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            || !long.TryParse(header.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var total)
            || last < first)
        {
            return false;
        }

        range = new BlobContentRange(first, last - first + 1, total);
        return true;
    }

    /// <summary>
    /// 分片上传不存在或已结束。
    /// </summary>
    private static bool IsNoSuchUpload(AmazonS3Exception exception) => exception.ErrorCode == "NoSuchUpload";

    /// <summary>
    /// 分片上传不存在或已结束。
    /// </summary>
    private static Error UploadNotFound(BlobMultipartUpload upload)
        => Error.NotFound(ObjectStorageErrors.UploadNotFound, ObjectStorageMessages.UploadNotFound, MessageArguments.Of(("container", upload.Container.Value), ("key", upload.Key.Value), ("upload", upload.UploadId)));

    /// <summary>
    /// 空的自定义元数据。
    /// </summary>
    private static IReadOnlyDictionary<string, string> EmptyMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 写一个对象。
    /// </summary>
    /// <remarks>
    /// 不覆盖时先 HEAD 再带条件写入（服务端支持条件写入时由它保证原子）；桶不存在且开启 <see cref="S3ObjectStorageOptions.EnsureBuckets"/> 时建桶后重试一次。
    /// </remarks>
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
    /// 把 SDK 的修改时间（UTC）转成带偏移的时刻。
    /// </summary>
    /// <remarks>
    /// 缺失时为最小值。
    /// </remarks>
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

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 文件系统对象存储。
/// </summary>
/// <remarks>
/// <c>&lt;root&gt;/&lt;container&gt;/data/&lt;key&gt;</c> 存内容，<c>&lt;root&gt;/&lt;container&gt;/meta/&lt;key&gt;.json</c> 存元数据（键的段按 <see cref="BlobPathEncoding"/> 编码，大小写不敏感的文件系统上仍区分大小写）。写入先落同目录的临时文件再原子替换，读取方要么看到旧对象要么看到完整的新对象；ETag 是内容的 SHA-256。配额在进程内计量：<see cref="InitializeAsync"/> 扫描目录得到已用量，之后每次写入与删除更新；多进程共享目录时只是近似。同时是健康项 <see cref="FileSystemObjectStorageFeature.HealthName"/> 的贡献者：已用量达到 <see cref="FileSystemObjectStorageOptions.MaxTotalBytes"/> 即 Degraded。范围读取打开内容文件后定位到起点、只交出该段；分片上传暂存在 <c>&lt;root&gt;/&lt;container&gt;/uploads/&lt;上传标识&gt;/</c>，完成时拼接成临时文件再原子替换（见 <see cref="FileSystemMultipartUploads"/>）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class FileSystemBlobStore : IBlobStore, IBlobMultipartStore, IHealthContributor
{
    /// <summary>
    /// 内容目录名。
    /// </summary>
    private const string DataDirectory = "data";

    /// <summary>
    /// 元数据目录名。
    /// </summary>
    private const string MetaDirectory = "meta";

    /// <summary>
    /// 临时文件的名字片段。
    /// </summary>
    /// <remarks>
    /// 列举时跳过。
    /// </remarks>
    internal const string TemporaryMarker = ".xihan-tmp-";

    /// <summary>
    /// 元数据文件后缀。
    /// </summary>
    private const string MetaSuffix = ".json";

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly FileSystemObjectStorageOptions _options;

    /// <summary>
    /// 时钟。
    /// </summary>
    /// <remarks>
    /// 元数据的修改时间来自它。
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 保护配额计量的锁。
    /// </summary>
    private readonly Lock _quota = new();

    /// <summary>
    /// 已用字节。
    /// </summary>
    /// <remarks>
    /// 负数表示尚未扫描。
    /// </remarks>
    private long _usedBytes = -1;

    /// <summary>
    /// 分片上传的暂存与拼接。
    /// </summary>
    private readonly FileSystemMultipartUploads _multipart;

    /// <summary>
    /// 创建。
    /// </summary>
    public FileSystemBlobStore(FileSystemObjectStorageOptions options, TimeProvider clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _multipart = new FileSystemMultipartUploads(this, options, clock);
    }

    /// <summary>
    /// 根目录。
    /// </summary>
    public string RootPath => _options.RootPath;

    /// <summary>
    /// 已用字节（进程内计量）。
    /// </summary>
    /// <remarks>
    /// 未扫描时为 0。
    /// </remarks>
    public long UsedBytes
    {
        get
        {
            lock (_quota)
            {
                return Math.Max(_usedBytes, 0);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 已用量达到总配额即 Degraded：此后任何写入都会被 QuotaExceeded 拒绝
        var used = UsedBytes;
        return
        [
            used >= _options.MaxTotalBytes
                ? new ContributorStatus(FileSystemObjectStorageFeature.HealthName, LifecycleState.Degraded, Error.Unavailable(ObjectStorageErrors.QuotaExceeded, ObjectStorageMessages.QuotaReached, MessageArguments.Of(("limit", _options.MaxTotalBytes), ("used", used))))
                : new ContributorStatus(FileSystemObjectStorageFeature.HealthName, LifecycleState.Ready),
        ];
    }

    /// <summary>
    /// 建根目录并扫描已用量，清掉过期（早于 <see cref="FileSystemObjectStorageOptions.MultipartExpiry"/>）的分片上传。
    /// </summary>
    /// <remarks>
    /// 可重复调用，每次重新扫描。
    /// </remarks>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.RootPath);
        long total = 0;
        foreach (var container in Directory.EnumerateDirectories(_options.RootPath))
        {
            _multipart.RemoveExpired(container, cancellationToken);
            var data = Path.Combine(container, DataDirectory);
            if (!Directory.Exists(data))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 上次崩溃留下的临时文件不算用量，顺手清掉
                if (file.Contains(TemporaryMarker, StringComparison.Ordinal))
                {
                    File.Delete(file);
                    continue;
                }

                total += new FileInfo(file).Length;
            }
        }

        lock (_quota)
        {
            _usedBytes = total;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<BlobPutResult>> PutAsync(ContainerName container, IReadOnlyList<BlobUpload> uploads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        EnsureInitialized();
        var results = new List<BlobPutResult>(uploads.Count);
        foreach (var upload in uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new BlobPutResult(upload.Key, await PutOneAsync(container, upload, cancellationToken).ConfigureAwait(false)));
        }

        return results;
    }

    /// <inheritdoc />
    public ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
        => ValueTask.FromResult(Open(container, key, null));

    /// <inheritdoc />
    public ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, BlobRange range, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        return ValueTask.FromResult(Open(container, key, range));
    }

    /// <inheritdoc />
    public ValueTask<Result<BlobMultipartUpload>> StartAsync(ContainerName container, BlobKey key, BlobPutOptions options, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _multipart.StartAsync(container, key, options, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<BlobPart>> UploadPartAsync(BlobMultipartUpload upload, int partNumber, Stream content, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _multipart.UploadPartAsync(upload, partNumber, content, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<BlobPart>>> ListPartsAsync(BlobMultipartUpload upload, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return ValueTask.FromResult(_multipart.ListParts(upload));
    }

    /// <inheritdoc />
    public ValueTask<Result<BlobMetadata>> CompleteAsync(BlobMultipartUpload upload, IReadOnlyList<BlobPart> parts, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _multipart.CompleteAsync(upload, parts, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result> AbortAsync(BlobMultipartUpload upload, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return ValueTask.FromResult(_multipart.Abort(upload));
    }

    /// <summary>
    /// 打开内容。
    /// </summary>
    /// <remarks>
    /// 整对象，或定位到范围起点并只交出该段（起点不在对象内时返回 <see cref="ObjectStorageErrors.RangeNotSatisfiable"/>）。
    /// </remarks>
    private Result<BlobContent> Open(ContainerName container, BlobKey key, BlobRange? range)
    {
        EnsureInitialized();
        try
        {
            var metadata = ReadMetadata(container, key);
            if (!metadata.TryGetValue(out var found))
            {
                return NotFound(container, key);
            }

            if (range is null)
            {
                var whole = new FileStream(DataPath(container, key), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                return Result.Success(new BlobContent(found, whole));
            }

            if (!range.Resolve(found.Length).TryGetValue(out var resolved))
            {
                return Error.Validation(ObjectStorageErrors.RangeNotSatisfiable, ObjectStorageMessages.RangeNotSatisfiable, MessageArguments.Of(("offset", range.Offset), ("length", found.Length)));
            }

            var file = new FileStream(DataPath(container, key), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 64 * 1024, FileOptions.Asynchronous);
            file.Seek(resolved.Offset, SeekOrigin.Begin);
            return Result.Success(new BlobContent(found, new BoundedReadStream(file, resolved.Length), resolved));
        }
        catch (FileNotFoundException)
        {
            return NotFound(container, key);
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound(container, key);
        }
        catch (IOException exception)
        {
            return Unavailable(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<BlobMetadata>> HeadAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        try
        {
            var metadata = ReadMetadata(container, key);
            return ValueTask.FromResult(metadata.TryGetValue(out var found) ? Result.Success(found) : NotFound(container, key));
        }
        catch (IOException exception)
        {
            return ValueTask.FromResult<Result<BlobMetadata>>(Unavailable(exception));
        }
        catch (UnauthorizedAccessException exception)
        {
            return ValueTask.FromResult<Result<BlobMetadata>>(Unavailable(exception));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<BlobDeleteResult>> DeleteAsync(ContainerName container, IReadOnlyList<BlobKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        EnsureInitialized();
        var results = new List<BlobDeleteResult>(keys.Count);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(new BlobDeleteResult(key, DeleteOne(container, key)));
        }

        return ValueTask.FromResult<IReadOnlyList<BlobDeleteResult>>(results);
    }

    /// <inheritdoc />
    public ValueTask<Result<BlobPage>> ListAsync(ContainerName container, BlobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureInitialized();
        try
        {
            var data = Path.Combine(_options.RootPath, container.Value, DataDirectory);
            if (!Directory.Exists(data))
            {
                return ValueTask.FromResult(Result.Success(new BlobPage([], null)));
            }

            // 目录遍历没有顺序保证：先收集全部匹配的键再序数排序，再按续页标记（上一页最后一个键）截取
            var after = query.ContinuationToken is { } token ? DecodeToken(token) : null;
            var keys = new List<string>();
            foreach (var file in Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Contains(TemporaryMarker, StringComparison.Ordinal))
                {
                    continue;
                }

                var key = BlobPathEncoding.ToKey(Path.GetRelativePath(data, file));
                if (BlobKey.IsValid(key) && key.StartsWith(query.Prefix, StringComparison.Ordinal) && (after is null || string.CompareOrdinal(key, after) > 0))
                {
                    keys.Add(key);
                }
            }

            keys.Sort(StringComparer.Ordinal);
            var items = new List<BlobMetadata>(Math.Min(keys.Count, query.PageSize));
            foreach (var key in keys.Take(query.PageSize))
            {
                // 列举期间被删除的对象跳过
                if (ReadMetadata(container, new BlobKey(key)).TryGetValue(out var metadata))
                {
                    items.Add(metadata);
                }
            }

            var next = keys.Count > query.PageSize ? EncodeToken(keys[query.PageSize - 1]) : null;
            return ValueTask.FromResult(Result.Success(new BlobPage(items, next)));
        }
        catch (IOException exception)
        {
            return ValueTask.FromResult<Result<BlobPage>>(Unavailable(exception));
        }
        catch (UnauthorizedAccessException exception)
        {
            return ValueTask.FromResult<Result<BlobPage>>(Unavailable(exception));
        }
    }

    /// <summary>
    /// 写一个对象。
    /// </summary>
    /// <remarks>
    /// 配额预检、流式复制到临时文件并计算 SHA-256、原子替换、写元数据、更新用量。
    /// </remarks>
    private async ValueTask<Result<BlobMetadata>> PutOneAsync(ContainerName container, BlobUpload upload, CancellationToken cancellationToken)
    {
        var dataPath = DataPath(container, upload.Key);
        var metaPath = MetaPath(container, upload.Key);
        var temporary = dataPath + TemporaryMarker + Guid.NewGuid().ToString("N");
        try
        {
            var existing = ReadMetadata(container, upload.Key);
            if (existing.HasValue && !upload.Options.Overwrite)
            {
                return Error.Conflict(ObjectStorageErrors.BlobExists, ObjectStorageMessages.BlobExists, MessageArguments.Of(("container", container.Value), ("key", upload.Key.Value)));
            }

            var previousLength = existing.TryGetValue(out var previous) ? previous.Length : 0;
            Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);

            long length = 0;
            string etag;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, FileOptions.Asynchronous);
                await using (file.ConfigureAwait(false))
                {
                    var buffer = new byte[64 * 1024];
                    int read;
                    while ((read = await upload.Content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        length += read;
                        // 单对象上限与总配额在复制过程中逐段检查，超限立即停止，不把超限的内容留在磁盘上
                        if (length > _options.MaxBlobBytes || !FitsQuota(length - previousLength))
                        {
                            return QuotaExceeded(length);
                        }

                        hash.AppendData(buffer, 0, read);
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }

                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                etag = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            return Result.Success(await CommitAsync(container, upload.Key, temporary, length, etag, upload.Options.ContentType, upload.Options.Metadata, previousLength, cancellationToken).ConfigureAwait(false));
        }
        catch (IOException exception)
        {
            return Unavailable(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unavailable(exception);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// 提交写好的临时内容文件。
    /// </summary>
    /// <remarks>
    /// 原子替换为对象内容、写元数据（同样先临时文件再替换）、按与旧对象的长度差更新用量。写入与分片上传的完成共用。
    /// </remarks>
    internal async ValueTask<BlobMetadata> CommitAsync(ContainerName container, BlobKey key, string temporary, long length, string etag, string contentType, IReadOnlyDictionary<string, string> metadata, long previousLength, CancellationToken cancellationToken)
    {
        var metaPath = MetaPath(container, key);
        Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
        File.Move(temporary, DataPath(container, key), overwrite: true);
        var document = new BlobMetadataDocument
        {
            Length = length,
            ContentType = contentType,
            ETag = etag,
            LastModified = _clock.GetUtcNow(),
            Metadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal),
        };
        var metaTemporary = metaPath + TemporaryMarker + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(metaTemporary, JsonSerializer.SerializeToUtf8Bytes(document, FileSystemJsonContext.Default.BlobMetadataDocument), cancellationToken).ConfigureAwait(false);
        File.Move(metaTemporary, metaPath, overwrite: true);

        lock (_quota)
        {
            _usedBytes += length - previousLength;
        }

        return ToMetadata(key, document);
    }

    /// <summary>
    /// 与对象内容同目录的新临时文件路径（同一卷上原子替换）。
    /// </summary>
    /// <remarks>
    /// 目录不存在时建出。
    /// </remarks>
    internal string TemporaryDataPath(ContainerName container, BlobKey key)
    {
        var dataPath = DataPath(container, key);
        Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
        return dataPath + TemporaryMarker + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 容器目录。
    /// </summary>
    internal string ContainerPath(ContainerName container) => Within(Path.Combine(_options.RootPath, container.Value));

    /// <summary>
    /// 删一个对象。
    /// </summary>
    /// <remarks>
    /// 内容与元数据都删，更新用量；返回是否存在过。
    /// </remarks>
    private Result<bool> DeleteOne(ContainerName container, BlobKey key)
    {
        try
        {
            // File.Delete 对不存在的文件不报错，但目录不存在会抛出：先判断存在再删
            var dataPath = DataPath(container, key);
            var existed = File.Exists(dataPath);
            var length = existed ? new FileInfo(dataPath).Length : 0;
            if (existed)
            {
                File.Delete(dataPath);
            }

            var metaPath = MetaPath(container, key);
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            if (existed)
            {
                lock (_quota)
                {
                    _usedBytes = Math.Max(0, _usedBytes - length);
                }
            }

            return Result.Success(existed);
        }
        catch (IOException exception)
        {
            return Unavailable(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 读元数据文件。
    /// </summary>
    /// <remarks>
    /// 内容文件在而元数据缺失（外部写入）时按文件信息补一份。
    /// </remarks>
    internal Maybe<BlobMetadata> ReadMetadata(ContainerName container, BlobKey key)
    {
        var dataPath = DataPath(container, key);
        if (!File.Exists(dataPath))
        {
            return default;
        }

        var metaPath = MetaPath(container, key);
        if (File.Exists(metaPath))
        {
            var document = JsonSerializer.Deserialize(File.ReadAllBytes(metaPath), FileSystemJsonContext.Default.BlobMetadataDocument);
            if (document is not null)
            {
                return ToMetadata(key, document);
            }
        }

        var info = new FileInfo(dataPath);
        return new BlobMetadata(key, info.Length, BlobPutOptions.DefaultContentType, info.LastWriteTimeUtc.Ticks.ToString("x", System.Globalization.CultureInfo.InvariantCulture), new DateTimeOffset(info.LastWriteTimeUtc), new Dictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// 元数据文件 → 元数据。
    /// </summary>
    private static BlobMetadata ToMetadata(BlobKey key, BlobMetadataDocument document)
        => new(key, document.Length, document.ContentType, document.ETag, document.LastModified, document.Metadata);

    /// <summary>
    /// 再写 <paramref name="delta"/> 字节是否仍在总配额内。
    /// </summary>
    internal bool FitsQuota(long delta)
    {
        lock (_quota)
        {
            return _usedBytes + delta <= _options.MaxTotalBytes;
        }
    }

    /// <summary>
    /// 没有扫描过时按空目录起算，让不经宿主直接构造的存储也能用。
    /// </summary>
    private void EnsureInitialized()
    {
        lock (_quota)
        {
            if (_usedBytes < 0)
            {
                _usedBytes = 0;
            }
        }

        Directory.CreateDirectory(_options.RootPath);
    }

    /// <summary>
    /// 内容文件路径。
    /// </summary>
    /// <remarks>
    /// 键的段按 <see cref="BlobPathEncoding"/> 编码后映射为子目录。结果必须落在根目录内（键文法已排除 <c>..</c>，这里再守一道）。
    /// </remarks>
    private string DataPath(ContainerName container, BlobKey key) => Within(Path.Combine(_options.RootPath, container.Value, DataDirectory, BlobPathEncoding.ToRelativePath(key)));

    /// <summary>
    /// 元数据文件路径。
    /// </summary>
    private string MetaPath(ContainerName container, BlobKey key) => Within(Path.Combine(_options.RootPath, container.Value, MetaDirectory, BlobPathEncoding.ToRelativePath(key) + MetaSuffix));

    /// <summary>
    /// 路径必须在根目录之内。
    /// </summary>
    private string Within(string path)
    {
        var full = Path.GetFullPath(path);
        return full.StartsWith(_options.RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : throw new InvalidOperationException($"Path '{full}' escapes the object storage root '{_options.RootPath}'.");
    }

    /// <summary>
    /// 续页标记：上一页最后一个键的 Base64Url。
    /// </summary>
    private static string EncodeToken(string lastKey) => Convert.ToBase64String(Encoding.UTF8.GetBytes(lastKey)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// 解析续页标记。
    /// </summary>
    /// <remarks>
    /// 不合法当作从头开始。
    /// </remarks>
    private static string? DecodeToken(string token)
    {
        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - (padded.Length % 4)) % 4);
            return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// 不存在。
    /// </summary>
    internal static Error NotFound(ContainerName container, BlobKey key)
        => Error.NotFound(ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.BlobNotFound, MessageArguments.Of(("container", container.Value), ("key", key.Value)));

    /// <summary>
    /// 超配额。
    /// </summary>
    internal Error QuotaExceeded(long requested)
        => Error.Unavailable(ObjectStorageErrors.QuotaExceeded, ObjectStorageMessages.QuotaExceeded, MessageArguments.Of(("limit", Math.Min(_options.MaxTotalBytes, _options.MaxBlobBytes)), ("requested", requested)));

    /// <summary>
    /// IO 失败。
    /// </summary>
    internal static Error Unavailable(Exception exception)
        => Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", exception.Message)));
}

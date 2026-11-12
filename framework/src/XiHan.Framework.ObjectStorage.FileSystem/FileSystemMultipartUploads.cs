// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 文件系统的分片上传
/// </summary>
/// <remarks>
/// 每次上传一个目录 <c>&lt;container&gt;/uploads/&lt;上传标识&gt;/</c>，<c>manifest.json</c> 记完成后对象的键与写入选项，分片文件名带编号与内容的 SHA-256（<c>00001.&lt;etag&gt;.part</c>），同一编号再传先落新文件再删旧的，所以 ETag 与内容永远对得上。完成时按清单拼接成对象同目录的临时文件，经存储的提交路径原子替换并计入用量，然后删掉上传目录；单片与拼接后的对象都受单对象上限约束
/// 暂存有界（ADR-0184）：分片边写边预留、计入总配额；未完成的上传数不超过 <see cref="FileSystemObjectStorageOptions.MaxMultipartUploads"/>；过期的上传拒绝再传分片与完成，碰到即清掉，存储运行期间发起新上传时也按周期清理
/// </remarks>
internal sealed class FileSystemMultipartUploads(FileSystemBlobStore store, FileSystemObjectStorageOptions options, TimeProvider clock)
{
    /// <summary>
    /// 运行期间清理过期上传的最长间隔
    /// </summary>
    private static readonly TimeSpan _maxSweepInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// 保护上传计数与清理时刻的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 未完成的上传数
    /// </summary>
    private int _open;

    /// <summary>
    /// 下次清理过期上传的时刻
    /// </summary>
    private DateTimeOffset _nextSweep = DateTimeOffset.MinValue;

    /// <summary>
    /// 上传目录名
    /// </summary>
    private const string UploadsDirectory = "uploads";

    /// <summary>
    /// 清单文件名
    /// </summary>
    private const string ManifestFile = "manifest.json";

    /// <summary>
    /// 分片文件后缀
    /// </summary>
    private const string PartSuffix = ".part";

    /// <summary>
    /// 发起
    /// </summary>
    /// <remarks>
    /// 不覆盖且对象已存在即冲突；到了清理周期先清掉过期的上传，再占一个上传名额（满了返回 <see cref="ObjectStorageErrors.QuotaExceeded"/>），建上传目录并原子写入清单
    /// </remarks>
    public async ValueTask<Result<BlobMultipartUpload>> StartAsync(ContainerName container, BlobKey key, BlobPutOptions putOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(putOptions);
        var claimed = false;
        try
        {
            if (!putOptions.Overwrite && store.ReadMetadata(container, key).HasValue)
            {
                return FileSystemBlobStore.Exists(container, key);
            }

            SweepIfDue(cancellationToken);
            lock (_gate)
            {
                // 名额在建目录之前占住：并发发起时合计也超不过上限
                if (_open >= options.MaxMultipartUploads)
                {
                    return Error.Unavailable(ObjectStorageErrors.QuotaExceeded, ObjectStorageMessages.UploadsExhausted, MessageArguments.Of(("limit", options.MaxMultipartUploads)));
                }

                _open++;
                claimed = true;
            }

            var uploadId = Guid.NewGuid().ToString("N");
            var directory = UploadPath(container, uploadId);
            Directory.CreateDirectory(directory);
            var manifest = new MultipartManifestDocument
            {
                Key = key.Value,
                ContentType = putOptions.ContentType,
                Metadata = new Dictionary<string, string>(putOptions.Metadata, StringComparer.Ordinal),
                Overwrite = putOptions.Overwrite,
                StartedAt = clock.GetUtcNow(),
            };
            var path = Path.Combine(directory, ManifestFile);
            var temporary = path + FileSystemBlobStore.TemporaryMarker + Guid.NewGuid().ToString("N");
            await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(manifest, FileSystemJsonContext.Default.MultipartManifestDocument), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
            claimed = false;
            return Result.Success(new BlobMultipartUpload(container, key, uploadId) { Overwrite = putOptions.Overwrite });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return store.Unavailable(exception);
        }
        finally
        {
            if (claimed)
            {
                // 清单没写成：上传没有建出来，名额让出去
                ReleaseSlot();
            }
        }
    }

    /// <summary>
    /// 上传一片
    /// </summary>
    /// <remarks>
    /// 流式落临时文件并算 SHA-256，逐段预留配额（超过单对象上限或总配额即停），改名为带 ETag 的分片文件并计入用量，再删掉同编号的旧版本并扣减
    /// </remarks>
    public async ValueTask<Result<BlobPart>> UploadPartAsync(BlobMultipartUpload upload, int partNumber, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(content);
        BlobMultipart.ValidatePartNumber(partNumber);
        if (!TryOpen(upload, out var directory, out _))
        {
            return UploadNotFound(upload);
        }

        var temporary = Path.Combine(directory, partNumber.ToString("D5", CultureInfo.InvariantCulture) + FileSystemBlobStore.TemporaryMarker + Guid.NewGuid().ToString("N"));
        long reserved = 0;
        try
        {
            long length = 0;
            string etag;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, FileOptions.Asynchronous);
                await using (file.ConfigureAwait(false))
                {
                    var buffer = new byte[64 * 1024];
                    int read;
                    while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        length += read;
                        // 暂存的分片同样占盘：与对象写入共用总配额，逐段预留
                        if (length > options.MaxBlobBytes || !store.TryReserve(read))
                        {
                            return store.QuotaExceeded(length);
                        }

                        reserved += read;
                        hash.AppendData(buffer, 0, read);
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }

                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                etag = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            var path = Path.Combine(directory, PartFileName(partNumber, etag));
            // 改名接手这份预留：成功即转成用量，失败也由它释放
            reserved = 0;
            store.ReplaceCounted(temporary, path, length);
            foreach (var stale in Directory.EnumerateFiles(directory, partNumber.ToString("D5", CultureInfo.InvariantCulture) + ".*" + PartSuffix))
            {
                if (!string.Equals(stale, path, StringComparison.Ordinal))
                {
                    store.DeleteCounted(stale);
                }
            }

            return Result.Success(new BlobPart(partNumber, etag, length));
        }
        catch (DirectoryNotFoundException)
        {
            // 上传期间被放弃或完成
            return UploadNotFound(upload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return store.Unavailable(exception);
        }
        finally
        {
            store.Release(reserved);
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// 已上传的分片，按编号升序
    /// </summary>
    public Result<IReadOnlyList<BlobPart>> ListParts(BlobMultipartUpload upload)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (!TryOpen(upload, out var directory, out _))
        {
            return UploadNotFound(upload);
        }

        try
        {
            return Result.Success<IReadOnlyList<BlobPart>>([.. Parts(directory).Values.OrderBy(static p => p.Part.PartNumber).Select(static p => p.Part)]);
        }
        catch (DirectoryNotFoundException)
        {
            return UploadNotFound(upload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return store.Unavailable(exception);
        }
    }

    /// <summary>
    /// 完成
    /// </summary>
    /// <remarks>
    /// 校验清单（非空、严格升序、每片已上传且 ETag 相符、非末片不小于最小分片），校验覆盖选项，为拼出的全长预留配额，拼接后经存储提交，再删上传目录。清单不合法时上传保留
    /// </remarks>
    public async ValueTask<Result<BlobMetadata>> CompleteAsync(BlobMultipartUpload upload, IReadOnlyList<BlobPart> parts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(parts);
        if (!TryOpen(upload, out var directory, out var manifest))
        {
            return UploadNotFound(upload);
        }

        if (parts.Count == 0)
        {
            return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsEmpty);
        }

        string? temporary = null;
        long reserved = 0;
        try
        {
            var uploaded = Parts(directory);
            var files = new List<string>(parts.Count);
            long total = 0;
            for (var index = 0; index < parts.Count; index++)
            {
                var part = parts[index] ?? throw new ArgumentException("The part list must not contain null.", nameof(parts));
                if (index > 0 && part.PartNumber <= parts[index - 1].PartNumber)
                {
                    return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsUnordered, MessageArguments.Of(("part", part.PartNumber)));
                }

                if (!uploaded.TryGetValue(part.PartNumber, out var found) || !string.Equals(found.Part.ETag, part.ETag, StringComparison.OrdinalIgnoreCase))
                {
                    return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartUnknown, MessageArguments.Of(("part", part.PartNumber)));
                }

                if (index < parts.Count - 1 && found.Part.Length < BlobMultipart.MinPartBytes)
                {
                    return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartTooSmall, MessageArguments.Of(("part", part.PartNumber), ("length", found.Part.Length), ("minimum", BlobMultipart.MinPartBytes)));
                }

                total += found.Part.Length;
                files.Add(found.Path);
            }

            var key = upload.Key;
            if (!manifest.Overwrite && store.ReadMetadata(upload.Container, key).HasValue)
            {
                return FileSystemBlobStore.Exists(upload.Container, key);
            }

            // 拼接期间分片与拼好的临时文件同时在盘上：为拼出的全长预留，提交时转成用量（ADR-0183）
            if (total > options.MaxBlobBytes || !store.TryReserve(total))
            {
                return store.QuotaExceeded(total);
            }

            reserved = total;

            temporary = store.TemporaryDataPath(upload.Container, key);
            string etag;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, FileOptions.Asynchronous);
                await using (target.ConfigureAwait(false))
                {
                    var buffer = new byte[64 * 1024];
                    foreach (var file in files)
                    {
                        var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        await using (source.ConfigureAwait(false))
                        {
                            int read;
                            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                hash.AppendData(buffer, 0, read);
                                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }

                    await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                etag = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            // 提交接手这份预留：成功即转成用量，失败也由它释放
            reserved = 0;
            var committed = await store.CommitAsync(upload.Container, key, temporary, total, etag, manifest.ContentType, manifest.Metadata, manifest.Overwrite, cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                // 不覆盖的完成被并发写入方抢先：上传保留（与先查到已存在时相同），调用方可以放弃或改为覆盖重传
                return committed;
            }

            RemoveUpload(directory);
            return committed;
        }
        catch (DirectoryNotFoundException)
        {
            return UploadNotFound(upload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return store.Unavailable(exception);
        }
        finally
        {
            store.Release(reserved);
            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// 放弃
    /// </summary>
    /// <remarks>
    /// 删掉上传目录、扣减暂存的分片并让出名额；不存在也成功
    /// </remarks>
    public Result Abort(BlobMultipartUpload upload)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (!TryOpen(upload, out var directory, out _))
        {
            return Result.Success();
        }

        try
        {
            RemoveUpload(directory);
            return Result.Success();
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return store.Unavailable(exception);
        }
    }

    /// <summary>
    /// 清掉一个容器里发起时间早于 <see cref="FileSystemObjectStorageOptions.MultipartExpiry"/> 的上传
    /// </summary>
    /// <remarks>
    /// 清单损坏或缺失的按目录修改时间判断；暂存的分片从用量里扣掉，有清单的让出名额
    /// </remarks>
    public void RemoveExpired(string containerPath, CancellationToken cancellationToken)
    {
        var uploads = Path.Combine(containerPath, UploadsDirectory);
        if (!Directory.Exists(uploads))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(uploads))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExpired(StartedAt(directory)))
            {
                try
                {
                    RemoveUpload(directory);
                }
                catch (DirectoryNotFoundException)
                {
                    // 别的调用先删掉了
                }
            }
        }
    }

    /// <summary>
    /// 数一个容器里未完成上传暂存的分片字节与上传数
    /// </summary>
    /// <remarks>
    /// 启动扫描用；顺手删掉上次崩溃留下的临时分片文件
    /// </remarks>
    public static (long StagedBytes, int OpenUploads) Scan(string containerPath, CancellationToken cancellationToken)
    {
        var uploads = Path.Combine(containerPath, UploadsDirectory);
        if (!Directory.Exists(uploads))
        {
            return (0, 0);
        }

        long staged = 0;
        var open = 0;
        foreach (var directory in Directory.EnumerateDirectories(uploads))
        {
            if (File.Exists(Path.Combine(directory, ManifestFile)))
            {
                open++;
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Contains(FileSystemBlobStore.TemporaryMarker, StringComparison.Ordinal))
                {
                    File.Delete(file);
                }
                else if (file.EndsWith(PartSuffix, StringComparison.Ordinal))
                {
                    staged += new FileInfo(file).Length;
                }
            }
        }

        return (staged, open);
    }

    /// <summary>
    /// 按启动扫描的结果重设未完成的上传数
    /// </summary>
    public void ResetOpenCount(int open)
    {
        lock (_gate)
        {
            _open = open;
        }
    }

    /// <summary>
    /// 到了清理周期就清掉全部容器里过期的上传
    /// </summary>
    /// <remarks>
    /// 周期取 <see cref="FileSystemObjectStorageOptions.MultipartExpiry"/> 与一小时中较短的；并发发起时只有一个调用去清理
    /// </remarks>
    private void SweepIfDue(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        lock (_gate)
        {
            if (now < _nextSweep)
            {
                return;
            }

            _nextSweep = now + (options.MultipartExpiry < _maxSweepInterval ? options.MultipartExpiry : _maxSweepInterval);
        }

        if (!Directory.Exists(options.RootPath))
        {
            return;
        }

        foreach (var container in Directory.EnumerateDirectories(options.RootPath))
        {
            RemoveExpired(container, cancellationToken);
        }
    }

    /// <summary>
    /// 删掉一个上传目录
    /// </summary>
    /// <remarks>
    /// 先在锁里删清单并让出名额（并发的放弃、完成与清理只有一个能让出），之后的调用看不到这个上传；再逐个删分片并扣减用量，最后删目录。删清单之后才落进来的分片随目录删掉、不扣减，用量只会多算不会少算
    /// </remarks>
    private void RemoveUpload(string directory)
    {
        var manifest = Path.Combine(directory, ManifestFile);
        lock (_gate)
        {
            if (File.Exists(manifest))
            {
                File.Delete(manifest);
                _open = Math.Max(0, _open - 1);
            }
        }

        foreach (var part in Directory.EnumerateFiles(directory, "*" + PartSuffix))
        {
            store.DeleteCounted(part);
        }

        Directory.Delete(directory, recursive: true);
    }

    /// <summary>
    /// 让出一个上传名额
    /// </summary>
    private void ReleaseSlot()
    {
        lock (_gate)
        {
            _open = Math.Max(0, _open - 1);
        }
    }

    /// <summary>
    /// 发起时刻早于保留期即过期
    /// </summary>
    private bool IsExpired(DateTimeOffset startedAt) => startedAt < clock.GetUtcNow() - options.MultipartExpiry;

    /// <summary>
    /// 上传是否存在且没有过期
    /// </summary>
    /// <remarks>
    /// 标识是本实现发出的格式、目录与清单都在、清单的键就是上传的键。过期的上传当作不存在并就地清掉：再传分片与完成都被拒绝，暂存的分片立即释放（ADR-0184）
    /// </remarks>
    private bool TryOpen(BlobMultipartUpload upload, out string directory, out MultipartManifestDocument manifest)
    {
        directory = string.Empty;
        manifest = null!;
        if (upload.UploadId.Length != 32 || !upload.UploadId.All(char.IsAsciiHexDigitLower))
        {
            return false;
        }

        directory = UploadPath(upload.Container, upload.UploadId);
        var path = Path.Combine(directory, ManifestFile);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var document = JsonSerializer.Deserialize(File.ReadAllBytes(path), FileSystemJsonContext.Default.MultipartManifestDocument);
            if (document is null || !string.Equals(document.Key, upload.Key.Value, StringComparison.Ordinal))
            {
                return false;
            }

            if (IsExpired(document.StartedAt))
            {
                RemoveUpload(directory);
                return false;
            }

            manifest = document;
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 上传目录里的分片：按编号取最近写入的那一版
    /// </summary>
    private static Dictionary<int, (BlobPart Part, string Path)> Parts(string directory)
    {
        var parts = new Dictionary<int, (BlobPart Part, string Path, DateTime Written)>();
        foreach (var file in Directory.EnumerateFiles(directory, "*" + PartSuffix))
        {
            var segments = Path.GetFileName(file).Split('.');
            if (segments.Length != 3 || !int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > BlobMultipart.MaxPartNumber)
            {
                continue;
            }

            var info = new FileInfo(file);
            if (!parts.TryGetValue(number, out var current) || info.LastWriteTimeUtc > current.Written)
            {
                parts[number] = (new BlobPart(number, segments[1], info.Length), file, info.LastWriteTimeUtc);
            }
        }

        return parts.ToDictionary(static p => p.Key, static p => (p.Value.Part, p.Value.Path));
    }

    /// <summary>
    /// 上传的发起时间：读清单，读不到按目录修改时间
    /// </summary>
    private static DateTimeOffset StartedAt(string directory)
    {
        try
        {
            var path = Path.Combine(directory, ManifestFile);
            if (File.Exists(path) && JsonSerializer.Deserialize(File.ReadAllBytes(path), FileSystemJsonContext.Default.MultipartManifestDocument) is { } manifest)
            {
                return manifest.StartedAt;
            }
        }
        catch (JsonException)
        {
            // 清单损坏：按目录时间判断
        }

        return new DateTimeOffset(Directory.GetLastWriteTimeUtc(directory), TimeSpan.Zero);
    }

    /// <summary>
    /// 上传目录
    /// </summary>
    private string UploadPath(ContainerName container, string uploadId) => Path.Combine(store.ContainerPath(container), UploadsDirectory, uploadId);

    /// <summary>
    /// 分片文件名：五位编号、内容的 SHA-256、后缀
    /// </summary>
    private static string PartFileName(int partNumber, string etag) => partNumber.ToString("D5", CultureInfo.InvariantCulture) + "." + etag + PartSuffix;

    /// <summary>
    /// 上传不存在或已结束
    /// </summary>
    private static Error UploadNotFound(BlobMultipartUpload upload)
        => Error.NotFound(ObjectStorageErrors.UploadNotFound, ObjectStorageMessages.UploadNotFound, MessageArguments.Of(("container", upload.Container.Value), ("key", upload.Key.Value), ("upload", upload.UploadId)));
}

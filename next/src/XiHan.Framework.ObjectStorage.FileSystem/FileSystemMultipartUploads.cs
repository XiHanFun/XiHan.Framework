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
/// 文件系统的分片上传。
/// </summary>
/// <remarks>
/// 每次上传一个目录 <c>&lt;container&gt;/uploads/&lt;上传标识&gt;/</c>，<c>manifest.json</c> 记完成后对象的键与写入选项，分片文件名带编号与内容的 SHA-256（<c>00001.&lt;etag&gt;.part</c>），同一编号再传先落新文件再删旧的，所以 ETag 与内容永远对得上。完成时按清单拼接成对象同目录的临时文件，经存储的提交路径原子替换并计入用量，然后删掉上传目录；暂存的分片不计入配额，单片与拼接后的对象都受单对象上限约束。
/// </remarks>
internal sealed class FileSystemMultipartUploads(FileSystemBlobStore store, FileSystemObjectStorageOptions options, TimeProvider clock)
{
    /// <summary>
    /// 上传目录名。
    /// </summary>
    private const string UploadsDirectory = "uploads";

    /// <summary>
    /// 清单文件名。
    /// </summary>
    private const string ManifestFile = "manifest.json";

    /// <summary>
    /// 分片文件后缀。
    /// </summary>
    private const string PartSuffix = ".part";

    /// <summary>
    /// 发起。
    /// </summary>
    /// <remarks>
    /// 不覆盖且对象已存在即冲突；建上传目录并原子写入清单。
    /// </remarks>
    public async ValueTask<Result<BlobMultipartUpload>> StartAsync(ContainerName container, BlobKey key, BlobPutOptions putOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(putOptions);
        try
        {
            if (!putOptions.Overwrite && store.ReadMetadata(container, key).HasValue)
            {
                return Error.Conflict(ObjectStorageErrors.BlobExists, ObjectStorageMessages.BlobExists, MessageArguments.Of(("container", container.Value), ("key", key.Value)));
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
            return Result.Success(new BlobMultipartUpload(container, key, uploadId, putOptions.Overwrite));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileSystemBlobStore.Unavailable(exception);
        }
    }

    /// <summary>
    /// 上传一片。
    /// </summary>
    /// <remarks>
    /// 流式落临时文件并算 SHA-256（超过单对象上限即停），改名为带 ETag 的分片文件，再删掉同编号的旧版本。
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
                        if (length > options.MaxBlobBytes)
                        {
                            return store.QuotaExceeded(length);
                        }

                        hash.AppendData(buffer, 0, read);
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }

                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                etag = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            var path = Path.Combine(directory, PartFileName(partNumber, etag));
            File.Move(temporary, path, overwrite: true);
            foreach (var stale in Directory.EnumerateFiles(directory, partNumber.ToString("D5", CultureInfo.InvariantCulture) + ".*" + PartSuffix))
            {
                if (!string.Equals(stale, path, StringComparison.Ordinal))
                {
                    File.Delete(stale);
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
            return FileSystemBlobStore.Unavailable(exception);
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
    /// 已上传的分片，按编号升序。
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
            return FileSystemBlobStore.Unavailable(exception);
        }
    }

    /// <summary>
    /// 完成。
    /// </summary>
    /// <remarks>
    /// 校验清单（非空、严格升序、每片已上传且 ETag 相符、非末片不小于最小分片），校验覆盖选项与配额，拼接后经存储提交，再删上传目录。清单不合法时上传保留。
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
            var existing = store.ReadMetadata(upload.Container, key);
            if (existing.HasValue && !manifest.Overwrite)
            {
                return Error.Conflict(ObjectStorageErrors.BlobExists, ObjectStorageMessages.BlobExists, MessageArguments.Of(("container", upload.Container.Value), ("key", key.Value)));
            }

            var previousLength = existing.TryGetValue(out var previous) ? previous.Length : 0;
            if (total > options.MaxBlobBytes || !store.FitsQuota(total - previousLength))
            {
                return store.QuotaExceeded(total);
            }

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

            var metadata = await store.CommitAsync(upload.Container, key, temporary, total, etag, manifest.ContentType, manifest.Metadata, previousLength, cancellationToken).ConfigureAwait(false);
            Directory.Delete(directory, recursive: true);
            return Result.Success(metadata);
        }
        catch (DirectoryNotFoundException)
        {
            return UploadNotFound(upload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileSystemBlobStore.Unavailable(exception);
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// 放弃。
    /// </summary>
    /// <remarks>
    /// 删掉上传目录；不存在也成功。
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
            Directory.Delete(directory, recursive: true);
            return Result.Success();
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileSystemBlobStore.Unavailable(exception);
        }
    }

    /// <summary>
    /// 清掉一个容器里发起时间早于 <see cref="FileSystemObjectStorageOptions.MultipartExpiry"/> 的上传。
    /// </summary>
    /// <remarks>
    /// 清单损坏或缺失的按目录修改时间判断。
    /// </remarks>
    public void RemoveExpired(string containerPath, CancellationToken cancellationToken)
    {
        var uploads = Path.Combine(containerPath, UploadsDirectory);
        if (!Directory.Exists(uploads))
        {
            return;
        }

        var cutoff = clock.GetUtcNow() - options.MultipartExpiry;
        foreach (var directory in Directory.EnumerateDirectories(uploads))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StartedAt(directory) < cutoff)
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// 上传是否存在：标识是本实现发出的格式、目录与清单都在、清单的键就是上传的键。
    /// </summary>
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

            manifest = document;
            return true;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 上传目录里的分片：按编号取最近写入的那一版。
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
    /// 上传的发起时间：读清单，读不到按目录修改时间。
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
    /// 上传目录。
    /// </summary>
    private string UploadPath(ContainerName container, string uploadId) => Path.Combine(store.ContainerPath(container), UploadsDirectory, uploadId);

    /// <summary>
    /// 分片文件名：五位编号、内容的 SHA-256、后缀。
    /// </summary>
    private static string PartFileName(int partNumber, string etag) => partNumber.ToString("D5", CultureInfo.InvariantCulture) + "." + etag + PartSuffix;

    /// <summary>
    /// 上传不存在或已结束。
    /// </summary>
    private static Error UploadNotFound(BlobMultipartUpload upload)
        => Error.NotFound(ObjectStorageErrors.UploadNotFound, ObjectStorageMessages.UploadNotFound, MessageArguments.Of(("container", upload.Container.Value), ("key", upload.Key.Value), ("upload", upload.UploadId)));
}

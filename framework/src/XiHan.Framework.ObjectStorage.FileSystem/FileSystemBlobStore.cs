// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 文件系统对象存储
/// </summary>
/// <remarks>
/// <c>&lt;root&gt;/&lt;container&gt;/data/&lt;key&gt;</c> 存内容，<c>&lt;root&gt;/&lt;container&gt;/meta/&lt;key&gt;.json</c> 存元数据（键的段按 <see cref="BlobPathEncoding"/> 编码，大小写不敏感的文件系统上仍区分大小写）。写入先落同目录的临时文件再原子替换，读取方要么看到旧对象要么看到完整的新对象；ETag 是内容的 SHA-256。配额在进程内计量：<see cref="InitializeAsync"/> 扫描目录得到已用量，之后每次写入与删除更新；多进程共享目录时只是近似。同时是健康项 <see cref="FileSystemObjectStorageFeature.HealthName"/> 的贡献者：已用量达到 <see cref="FileSystemObjectStorageOptions.MaxTotalBytes"/> 即 Degraded。范围读取打开内容文件后定位到起点、只交出该段；分片上传暂存在 <c>&lt;root&gt;/&lt;container&gt;/uploads/&lt;上传标识&gt;/</c>，完成时拼接成临时文件再原子替换（见 <see cref="FileSystemMultipartUploads"/>）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class FileSystemBlobStore : IBlobStore, IBlobMultipartStore, IHealthContributor
{
    /// <summary>
    /// 内容目录名
    /// </summary>
    private const string DataDirectory = "data";

    /// <summary>
    /// 元数据目录名
    /// </summary>
    private const string MetaDirectory = "meta";

    /// <summary>
    /// 临时文件的名字片段
    /// </summary>
    /// <remarks>
    /// 以 <c>~</c> 开头：键的编码只产出小写字母、数字、<c>._-</c> 与 <c>%XX</c>，对象的文件名里不会出现它，键里写什么都不会被当作临时文件（ADR-0212）。列举时跳过，启动时清掉
    /// </remarks>
    internal const string TemporaryMarker = "~xihan-tmp-";

    /// <summary>
    /// 旧版临时文件的名字片段
    /// </summary>
    /// <remarks>
    /// ADR-0212 之前的写法，后面跟 32 位十六进制。只用于启动时清掉旧版崩溃留下的临时文件
    /// </remarks>
    private const string LegacyTemporaryMarker = ".xihan-tmp-";

    /// <summary>
    /// 小写十六进制数字
    /// </summary>
    private static readonly SearchValues<char> _lowerHexDigits = SearchValues.Create("0123456789abcdef");

    /// <summary>
    /// 元数据文件后缀
    /// </summary>
    private const string MetaSuffix = ".json";

    /// <summary>
    /// 选项
    /// </summary>
    private readonly FileSystemObjectStorageOptions _options;

    /// <summary>
    /// 时钟
    /// </summary>
    /// <remarks>
    /// 元数据的修改时间来自它
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 保护配额计量的锁
    /// </summary>
    private readonly Lock _quota = new();

    /// <summary>
    /// 已用字节
    /// </summary>
    /// <remarks>
    /// 负数表示尚未扫描
    /// </remarks>
    private long _usedBytes = -1;

    /// <summary>
    /// 正在写入、尚未提交的字节
    /// </summary>
    /// <remarks>
    /// 写入方边复制边预留，提交时转成已用量，失败时释放；总配额按已用量加预留量判定，并发写入合计也超不过它（ADR-0183）
    /// </remarks>
    private long _reservedBytes;

    /// <summary>
    /// 分片上传的暂存与拼接
    /// </summary>
    private readonly FileSystemMultipartUploads _multipart;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建，尚未扫描已用量
    /// </summary>
    /// <remarks>
    /// 只在包内使用：特性与 Provider 构造后紧接着 <see cref="InitializeAsync"/>，外部经 <see cref="CreateAsync"/> 一步拿到初始化好的存储（ADR-0163）
    /// </remarks>
    internal FileSystemBlobStore(FileSystemObjectStorageOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<FileSystemBlobStore>();
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _multipart = new FileSystemMultipartUploads(this, options, clock);
    }

    /// <summary>
    /// 根目录
    /// </summary>
    public string RootPath => _options.RootPath;

    /// <summary>
    /// 创建并初始化
    /// </summary>
    /// <remarks>
    /// 建根目录、扫描已用量并清掉过期的分片上传后才交出：没有扫描过的存储配额从零算起，会放过超额的写入，所以不提供不初始化的公开构造（ADR-0163）。目录无权限或磁盘错误照常抛出
    /// </remarks>
    public static async Task<FileSystemBlobStore> CreateAsync(FileSystemObjectStorageOptions options, TimeProvider clock, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var store = new FileSystemBlobStore(options, clock, loggerFactory);
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return store;
    }

    /// <summary>
    /// 已用字节（进程内计量）
    /// </summary>
    /// <remarks>
    /// 未扫描时为 0
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
        var status = used >= _options.MaxTotalBytes
            ? new ContributorStatus(FileSystemObjectStorageFeature.HealthName, LifecycleState.Degraded)
            {
                Error = Error.Unavailable(ObjectStorageErrors.QuotaExceeded, ObjectStorageMessages.QuotaReached, MessageArguments.Of(("limit", _options.MaxTotalBytes), ("used", used))),
            }
            : new ContributorStatus(FileSystemObjectStorageFeature.HealthName, LifecycleState.Ready);
        return [status with { CheckedAt = _clock.GetUtcNow(), Data = new Dictionary<string, object?>(StringComparer.Ordinal) { ["usedBytes"] = used, ["maxTotalBytes"] = _options.MaxTotalBytes } }];
    }

    /// <summary>
    /// 建根目录并扫描已用量，清掉过期（早于 <see cref="FileSystemObjectStorageOptions.MultipartExpiry"/>）的分片上传
    /// </summary>
    /// <remarks>
    /// 可重复调用，每次重新扫描。已用量含对象内容与未完成上传暂存的分片，并数出未完成的上传（ADR-0184）
    /// </remarks>
    internal Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.RootPath);
        long total = 0;
        var uploads = 0;
        foreach (var container in Directory.EnumerateDirectories(_options.RootPath))
        {
            _multipart.RemoveExpired(container, cancellationToken);
            var (staged, open) = FileSystemMultipartUploads.Scan(container, cancellationToken);
            total += staged;
            uploads += open;
            var data = Path.Combine(container, DataDirectory);
            if (!Directory.Exists(data))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 上次崩溃留下的临时文件不算用量，顺手清掉。旧版的临时文件名与键的编码会重合，没有元数据文件的才是临时文件
                if (IsTemporary(file) || (IsLegacyTemporary(file) && !File.Exists(Path.Combine(container, MetaDirectory, Path.GetRelativePath(data, file) + MetaSuffix))))
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

        _multipart.ResetOpenCount(uploads);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<BlobMetadata>>> PutAsync(ContainerName container, IReadOnlyList<BlobUpload> uploads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        EnsureInitialized();
        var results = new List<Result<BlobMetadata>>(uploads.Count);
        foreach (var upload in uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await PutOneAsync(container, upload, cancellationToken).ConfigureAwait(false));
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
    /// 打开内容
    /// </summary>
    /// <remarks>
    /// 整对象，或定位到范围起点并只交出该段（起点不在对象内时返回 <see cref="ObjectStorageErrors.RangeNotSatisfiable"/>）
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
    public ValueTask<IReadOnlyList<Result<bool>>> DeleteAsync(ContainerName container, IReadOnlyList<BlobKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        EnsureInitialized();
        var results = new List<Result<bool>>(keys.Count);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(DeleteOne(container, key));
        }

        return ValueTask.FromResult<IReadOnlyList<Result<bool>>>(results);
    }

    /// <inheritdoc />
    public ValueTask<Result<BlobPage>> ListAsync(ContainerName container, BlobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 页大小越界是调用方可以改正的输入（RFC-0012）：以校验错误返回，不碰磁盘
        if (query.Validate() is { IsFailure: true } invalid)
        {
            return ValueTask.FromResult<Result<BlobPage>>(invalid.Error!);
        }

        EnsureInitialized();
        try
        {
            var data = Path.Combine(_options.RootPath, container.Value, DataDirectory);
            if (!Directory.Exists(data))
            {
                return ValueTask.FromResult(Result.Success(new BlobPage { Items = [] }));
            }

            // 目录遍历没有顺序保证：先收集全部匹配的键再序数排序，再按续页标记（上一页最后一个键）截取
            var after = query.ContinuationToken is { } token ? DecodeToken(token) : null;
            var keys = new List<string>();
            foreach (var file in Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsTemporary(file))
                {
                    continue;
                }

                var key = BlobPathEncoding.ToKey(Path.GetRelativePath(data, file));
                // 不符合键文法的文件（外部放入的、保留前缀下的框架对象，ADR-0090）不是本契约的对象，跳过
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
            return ValueTask.FromResult(Result.Success(new BlobPage { Items = items, ContinuationToken = next }));
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
    /// 写一个对象
    /// </summary>
    /// <remarks>
    /// 流式复制到临时文件并计算 SHA-256、逐段预留额度、原子替换（不覆盖时原子占位）、写元数据、把预留转成用量。覆盖时新旧内容同时在盘上，预留按新内容的全长，旧对象的字节到提交时才释放
    /// </remarks>
    private async ValueTask<Result<BlobMetadata>> PutOneAsync(ContainerName container, BlobUpload upload, CancellationToken cancellationToken)
    {
        var dataPath = DataPath(container, upload.Key);
        var metaPath = MetaPath(container, upload.Key);
        var temporary = dataPath + TemporaryMarker + Guid.NewGuid().ToString("N");
        long reserved = 0;
        try
        {
            if (!upload.Options.Overwrite && ReadMetadata(container, upload.Key).HasValue)
            {
                return Exists(container, upload.Key);
            }

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
                        // 单对象上限与总配额在复制过程中逐段检查、逐段预留，超限立即停止，不把超限的内容留在磁盘上
                        if (length > _options.MaxBlobBytes || !TryReserve(read))
                        {
                            return QuotaExceeded(length);
                        }

                        reserved += read;

                        hash.AppendData(buffer, 0, read);
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }

                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                etag = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            // 提交接手这份预留：成功即转成用量，失败也由它释放
            reserved = 0;
            return await CommitAsync(container, upload.Key, temporary, length, etag, upload.Options.ContentType, upload.Options.Metadata, upload.Options.Overwrite, cancellationToken).ConfigureAwait(false);
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
            Release(reserved);
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// 提交写好的临时内容文件
    /// </summary>
    /// <remarks>
    /// 调用方已为 <paramref name="length"/> 预留了额度，本方法接手：成功转成用量，失败释放。原子替换为对象内容、写元数据（同样先临时文件再替换）。写入与分片上传的完成共用。替换与记账在配额锁里一并完成，按此刻磁盘上旧内容的实际长度计算差值，并发覆盖与删除同一个键时用量与磁盘一致（ADR-0183）。<paramref name="overwrite"/> 为 <see langword="false"/> 时由文件系统原子判定谁先占住内容路径：Windows 上不替换地改名，其他平台先以独占创建的空文件占位、再把内容原子替换上去；内容文件已存在（并发的另一个写入方先占了位）返回 <see cref="ObjectStorageErrors.BlobExists"/>，不写元数据、不计用量（ADR-0168、ADR-0172）
    /// </remarks>
    internal async ValueTask<Result<BlobMetadata>> CommitAsync(ContainerName container, BlobKey key, string temporary, long length, string etag, string contentType, IReadOnlyDictionary<string, string> metadata, bool overwrite, CancellationToken cancellationToken)
    {
        var metaPath = MetaPath(container, key);
        var dataPath = DataPath(container, key);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
            // 改名只是元数据操作，锁里只放它与记账；复制与写元数据都在锁外
            lock (_quota)
            {
                if (ReplaceContent(container, key, temporary, dataPath, length, overwrite) is { } exists)
                {
                    _reservedBytes -= length;
                    return exists;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 没换上去：预留原样释放，异常交给调用方按基础设施失败报告
            Release(length);
            throw;
        }

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
        return Result.Success(ToMetadata(key, document));
    }

    /// <summary>
    /// 把临时内容文件换上去，并把预留转成用量
    /// </summary>
    /// <remarks>
    /// 调用方持有配额锁。不覆盖且内容文件已存在时返回冲突，不动用量与预留（由调用方释放）；成功为空
    /// </remarks>
    private Error? ReplaceContent(ContainerName container, BlobKey key, string temporary, string dataPath, long length, bool overwrite)
    {
        // 旧内容按此刻磁盘上的实际长度计：调用方开始写入时读到的元数据可能已被并发的覆盖或删除改掉
        var previous = File.Exists(dataPath) ? new FileInfo(dataPath).Length : 0;
        if (overwrite)
        {
            File.Move(temporary, dataPath, overwrite: true);
        }
        else if (OperatingSystem.IsWindows())
        {
            try
            {
                // 调用方先查过元数据，但查与写之间另一个写入方可能已经写入，要由文件系统原子判定谁先占位
                // Windows 的不替换改名（MoveFileEx 不带 MOVEFILE_REPLACE_EXISTING）是一次原子操作，目标已存在就失败；
                // 不用占位再替换：替换要求目标此刻没有被打开，并发写入方探测占位文件的那一下就让占到位的一方以拒绝访问失败
                File.Move(temporary, dataPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(dataPath))
            {
                return Exists(container, key);
            }
        }
        else
        {
            try
            {
                // 不用不替换的移动：.NET 在 Unix 上把它实现为先查目标再改名，两步之间别的写入方能抢先，后到的静默覆盖先到的；
                // 独占创建（O_CREAT | O_EXCL）在本地文件系统上原子，目标已存在就失败
                using (new FileStream(dataPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }
            }
            catch (IOException) when (File.Exists(dataPath))
            {
                return Exists(container, key);
            }

            // 占到位的才替换：rename 覆盖本进程刚建的空文件，与目标是否被打开无关；读取方要么看不到对象（元数据还没写），要么看到完整内容
            File.Move(temporary, dataPath, overwrite: true);
        }

        _reservedBytes -= length;
        _usedBytes += length - previous;
        return null;
    }

    /// <summary>
    /// 与对象内容同目录的新临时文件路径（同一卷上原子替换）
    /// </summary>
    /// <remarks>
    /// 目录不存在时建出
    /// </remarks>
    internal string TemporaryDataPath(ContainerName container, BlobKey key)
    {
        var dataPath = DataPath(container, key);
        Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
        return dataPath + TemporaryMarker + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 是不是临时文件
    /// </summary>
    /// <remarks>
    /// 只看文件名：所在目录（键的前缀段）与根路径里出现同样的文字不算
    /// </remarks>
    internal static bool IsTemporary(string path) => Path.GetFileName(path).Contains(TemporaryMarker, StringComparison.Ordinal);

    /// <summary>
    /// 文件名是不是旧版临时文件的样子
    /// </summary>
    /// <remarks>
    /// 以 <c>.xihan-tmp-</c> 加 32 位小写十六进制结尾。键也能写成这个样子，调用方还要看有没有元数据文件
    /// </remarks>
    internal static bool IsLegacyTemporary(string path)
    {
        var name = Path.GetFileName(path.AsSpan());
        const int Suffix = 32;
        if (name.Length <= LegacyTemporaryMarker.Length + Suffix)
        {
            return false;
        }

        var tail = name[^(LegacyTemporaryMarker.Length + Suffix)..];
        return tail.StartsWith(LegacyTemporaryMarker, StringComparison.Ordinal) && !tail[LegacyTemporaryMarker.Length..].ContainsAnyExcept(_lowerHexDigits);
    }

    /// <summary>
    /// 容器目录
    /// </summary>
    internal string ContainerPath(ContainerName container) => Within(Path.Combine(_options.RootPath, container.Value));

    /// <summary>
    /// 删一个对象
    /// </summary>
    /// <remarks>
    /// 内容与元数据都删，更新用量；返回是否存在过
    /// </remarks>
    private Result<bool> DeleteOne(ContainerName container, BlobKey key)
    {
        try
        {
            // File.Delete 对不存在的文件不报错，但目录不存在会抛出：先判断存在再删
            var dataPath = DataPath(container, key);
            bool existed;
            // 量长度、删除与记账在配额锁里一并完成：并发的覆盖提交要么在前（量到它的长度），要么在后（看到文件已删）
            lock (_quota)
            {
                existed = File.Exists(dataPath);
                if (existed)
                {
                    var length = new FileInfo(dataPath).Length;
                    File.Delete(dataPath);
                    _usedBytes = Math.Max(0, _usedBytes - length);
                }
            }

            var metaPath = MetaPath(container, key);
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
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
    /// 读元数据文件
    /// </summary>
    /// <remarks>
    /// 内容文件在而元数据缺失（外部写入）时按文件信息补一份
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
        return new BlobMetadata
        {
            Key = key,
            Length = info.Length,
            ContentType = BlobPutOptions.DefaultContentType,
            ETag = info.LastWriteTimeUtc.Ticks.ToString("x", System.Globalization.CultureInfo.InvariantCulture),
            LastModified = new DateTimeOffset(info.LastWriteTimeUtc),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// 元数据文件 → 元数据
    /// </summary>
    private static BlobMetadata ToMetadata(BlobKey key, BlobMetadataDocument document)
        => new()
        {
            Key = key,
            Length = document.Length,
            ContentType = document.ContentType,
            ETag = document.ETag,
            LastModified = document.LastModified,
            Metadata = document.Metadata,
        };

    /// <summary>
    /// 为将要写入的 <paramref name="bytes"/> 字节预留额度
    /// </summary>
    /// <remarks>
    /// 已用量加全部预留量仍在总配额内才预留；返回是否预留成功
    /// </remarks>
    internal bool TryReserve(long bytes)
    {
        lock (_quota)
        {
            if (Math.Max(_usedBytes, 0) + _reservedBytes + bytes > _options.MaxTotalBytes)
            {
                return false;
            }

            _reservedBytes += bytes;
            return true;
        }
    }

    /// <summary>
    /// 把临时文件换到 <paramref name="path"/>，并把它的预留转成用量
    /// </summary>
    /// <remarks>
    /// 调用方已为 <paramref name="length"/> 预留了额度。改名与记账在配额锁里一并完成：目标已存在时按它此刻的实际长度扣减（同一编号、同样内容的分片再传会替换同名文件）。改名失败时释放预留并抛出
    /// </remarks>
    internal void ReplaceCounted(string temporary, string path, long length)
    {
        try
        {
            lock (_quota)
            {
                var previous = File.Exists(path) ? new FileInfo(path).Length : 0;
                File.Move(temporary, path, overwrite: true);
                _reservedBytes -= length;
                _usedBytes += length - previous;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Release(length);
            throw;
        }
    }

    /// <summary>
    /// 删掉一个计入用量的文件，并扣减它此刻的实际长度
    /// </summary>
    /// <remarks>
    /// 量长度、删除与记账在配额锁里一并完成，并发删除同一个文件只扣一次；文件不存在时什么也不做
    /// </remarks>
    internal void DeleteCounted(string path)
    {
        lock (_quota)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var length = new FileInfo(path).Length;
            File.Delete(path);
            _usedBytes = Math.Max(0, _usedBytes - length);
        }
    }

    /// <summary>
    /// 释放没有用上的预留
    /// </summary>
    internal void Release(long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        lock (_quota)
        {
            _reservedBytes -= bytes;
        }
    }

    /// <summary>
    /// 没有扫描过时按空目录起算，让不经宿主直接构造的存储也能用
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
    /// 内容文件路径
    /// </summary>
    /// <remarks>
    /// 键的段按 <see cref="BlobPathEncoding"/> 编码后映射为子目录。结果必须落在根目录内（键文法已排除 <c>..</c>，这里再守一道）
    /// </remarks>
    private string DataPath(ContainerName container, BlobKey key) => Within(Path.Combine(_options.RootPath, container.Value, DataDirectory, BlobPathEncoding.ToRelativePath(key)));

    /// <summary>
    /// 元数据文件路径
    /// </summary>
    private string MetaPath(ContainerName container, BlobKey key) => Within(Path.Combine(_options.RootPath, container.Value, MetaDirectory, BlobPathEncoding.ToRelativePath(key) + MetaSuffix));

    /// <summary>
    /// 路径必须在根目录之内
    /// </summary>
    private string Within(string path)
    {
        var full = Path.GetFullPath(path);
        // 根路径已去掉结尾分隔符，只有文件系统的根目录（C:\、/）还以分隔符结尾，不能再补一个
        var root = _options.RootPath;
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal)
            ? full
            : throw new InvalidOperationException($"Path '{full}' escapes the object storage root '{_options.RootPath}'.");
    }

    /// <summary>
    /// 续页标记：上一页最后一个键的 Base64Url
    /// </summary>
    private static string EncodeToken(string lastKey) => Convert.ToBase64String(Encoding.UTF8.GetBytes(lastKey)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// 解析续页标记
    /// </summary>
    /// <remarks>
    /// 不合法当作从头开始
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
    /// 不存在
    /// </summary>
    internal static Error NotFound(ContainerName container, BlobKey key)
        => Error.NotFound(ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.BlobNotFound, MessageArguments.Of(("container", container.Value), ("key", key.Value)));

    /// <summary>
    /// 已存在且不允许覆盖
    /// </summary>
    internal static Error Exists(ContainerName container, BlobKey key)
        => Error.Conflict(ObjectStorageErrors.BlobExists, ObjectStorageMessages.BlobExists, MessageArguments.Of(("container", container.Value), ("key", key.Value)));

    /// <summary>
    /// 超配额
    /// </summary>
    internal Error QuotaExceeded(long requested)
        => Error.Unavailable(ObjectStorageErrors.QuotaExceeded, ObjectStorageMessages.QuotaExceeded, MessageArguments.Of(("limit", Math.Min(_options.MaxTotalBytes, _options.MaxBlobBytes)), ("requested", requested)));

    /// <summary>
    /// IO 失败
    /// </summary>
    /// <remarks>
    /// IO 异常的消息常带服务器上的完整路径，而这个错误会返回给调用方：<c>reason</c> 只写类型，完整异常只进日志
    /// </remarks>
    internal Error Unavailable(Exception exception)
    {
        LogIoFailed(_logger, exception);
        var type = exception.GetType();
        return Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", type.FullName ?? type.Name)));
    }

    /// <summary>
    /// IO 失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "A file system operation of the blob store failed.")]
    private static partial void LogIoFailed(ILogger logger, Exception exception);
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 分片上传的批量放弃（ADR-0266）
/// </summary>
/// <remarks>
/// 逐项结果与入参同序，放弃的上传让出暂存的字节与名额；不存在的与重复的上传同样成功；两项之间检查取消，已放弃的不恢复；一项的 IO 失败只记在该项上
/// </remarks>
public sealed class FileSystemMultipartAbortTests : IDisposable
{
    /// <summary>
    /// 容器
    /// </summary>
    private static readonly ContainerName _container = new("uploads");

    /// <summary>
    /// 本用例的临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <summary>
    /// 取时刻时可以插入动作的时钟
    /// </summary>
    private readonly CallbackClock _clock = new(new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero));

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task Abort_IsBatch_ReportsEachUpload_AndFreesStagedBytesAndSlots()
    {
        var store = Store(maxUploads: 3);
        var first = await StartAsync(store, "a", stagedBytes: 300 * 1024);
        var second = await StartAsync(store, "b", stagedBytes: 100 * 1024);
        var kept = await StartAsync(store, "c", stagedBytes: 50 * 1024);
        // 标识合法但没有发起过，以及根本不是本存储给出的标识：都当作不存在
        var neverStarted = new BlobMultipartUpload(_container, new BlobKey("never"), "0123456789abcdef0123456789abcdef") { Overwrite = true };
        var foreign = new BlobMultipartUpload(_container, new BlobKey("foreign"), "not-issued-by-this-store") { Overwrite = true };

        var results = await store.AbortAsync([first, neverStarted, second, first, foreign], CancellationToken.None);

        // 与入参等长同序；不存在的、同一批里重复的都成功（放弃是幂等的）
        Assert.Equal(5, results.Count);
        Assert.All(results, static result => Assert.True(result.IsSuccess, result.Error?.ToString()));
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await store.ListPartsAsync(first, CancellationToken.None)).Error?.Code);
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await store.ListPartsAsync(second, CancellationToken.None)).Error?.Code);
        Assert.True((await store.ListPartsAsync(kept, CancellationToken.None)).IsSuccess);
        Assert.Equal(50 * 1024, store.UsedBytes);
        Assert.Equal([kept.UploadId], Directory.EnumerateDirectories(UploadsPath).Select(Path.GetFileName));

        // 让出了两个名额：还能再发起两个，第三个被上限拒绝
        Assert.True((await store.StartAsync(_container, new BlobKey("d"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.True((await store.StartAsync(_container, new BlobKey("e"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal(ObjectStorageMessages.UploadsExhausted, (await store.StartAsync(_container, new BlobKey("f"), BlobPutOptions.Default, CancellationToken.None)).Error?.MessageKey);
    }

    [Fact]
    public async Task Abort_EmptyBatch_ReturnsEmpty_AndTheSingleFormIsTheBatchOfOne()
    {
        var store = Store();
        var upload = await StartAsync(store, "single", stagedBytes: 10 * 1024);

        Assert.Empty(await store.AbortAsync([], CancellationToken.None));
        Assert.Equal(10 * 1024, store.UsedBytes);

        // 单条是批量的特例：扩展方法转调批量方法，取第一项
        Assert.True((await store.AbortAsync(upload, CancellationToken.None)).IsSuccess);
        Assert.True((await store.AbortAsync(upload, CancellationToken.None)).IsSuccess);
        Assert.Equal(0, store.UsedBytes);
    }

    [Fact]
    public async Task Abort_ANullUpload_ThrowsBeforeAbortingAnything()
    {
        var store = Store();
        var upload = await StartAsync(store, "kept", stagedBytes: 10 * 1024);

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.AbortAsync([upload, null!], CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await store.AbortAsync((IReadOnlyList<BlobMultipartUpload>)null!, CancellationToken.None));

        Assert.True((await store.ListPartsAsync(upload, CancellationToken.None)).IsSuccess);
        Assert.Equal(10 * 1024, store.UsedBytes);
    }

    [Fact]
    public async Task Abort_ChecksCancellationBeforeEachUpload_AndKeepsWhatWasAlreadyAborted()
    {
        var store = Store();
        var first = await StartAsync(store, "a", stagedBytes: 10 * 1024);
        var second = await StartAsync(store, "b", stagedBytes: 20 * 1024);
        var third = await StartAsync(store, "c", stagedBytes: 30 * 1024);
        using var cancellation = new CancellationTokenSource();
        // 放弃第一个上传时要取时刻判断它是否过期：在那一刻取消，第一个照常放弃完，第二个之前的检查抛出
        _clock.OnRead = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.AbortAsync([first, second, third], cancellation.Token));
        _clock.OnRead = null;

        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await store.ListPartsAsync(first, CancellationToken.None)).Error?.Code);
        Assert.True((await store.ListPartsAsync(second, CancellationToken.None)).IsSuccess);
        Assert.True((await store.ListPartsAsync(third, CancellationToken.None)).IsSuccess);
        Assert.Equal(50 * 1024, store.UsedBytes);

        // 放弃是幂等的：整批重试，已放弃的那一项同样成功
        var retried = await store.AbortAsync([first, second, third], CancellationToken.None);
        Assert.All(retried, static result => Assert.True(result.IsSuccess, result.Error?.ToString()));
        Assert.Equal(0, store.UsedBytes);
    }

    [Fact]
    public async Task Abort_WhenOneUploadCannotBeRemoved_ReportsItOnThatItem_AndStillAbortsTheRest()
    {
        // 只有 Windows 上打开着的文件删不掉；别的系统上没有不依赖账号权限的办法让删除失败
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = Store();
        var first = await StartAsync(store, "a", stagedBytes: 10 * 1024);
        var locked = await StartAsync(store, "b", stagedBytes: 20 * 1024);
        var third = await StartAsync(store, "c", stagedBytes: 30 * 1024);
        var part = Directory.EnumerateFiles(Path.Combine(UploadsPath, locked.UploadId), "*.part").Single();

        IReadOnlyList<Primitives.Result> results;
        await using (new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            results = await store.AbortAsync([first, locked, third], CancellationToken.None);
        }

        Assert.Equal([true, false, true], results.Select(static result => result.IsSuccess));
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, results[1].Error?.Code);
        Assert.Equal(typeof(IOException).FullName, results[1].Error?.Details?["reason"]);
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await store.ListPartsAsync(first, CancellationToken.None)).Error?.Code);
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await store.ListPartsAsync(third, CancellationToken.None)).Error?.Code);
    }

    /// <summary>
    /// 容器里分片上传的暂存目录
    /// </summary>
    private string UploadsPath => Path.Combine(_root.Path, _container.Value, "uploads");

    /// <summary>
    /// 发起一个上传并暂存一片
    /// </summary>
    private static async Task<BlobMultipartUpload> StartAsync(FileSystemBlobStore store, string key, int stagedBytes)
    {
        var upload = (await store.StartAsync(_container, new BlobKey(key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        (await store.UploadPartAsync(upload, 1, new MemoryStream(new byte[stagedBytes]), CancellationToken.None)).ValueOrThrow();
        return upload;
    }

    /// <summary>
    /// 以给定的未完成上传数上限创建存储（分片上传保留 24 小时）
    /// </summary>
    private FileSystemBlobStore Store(int maxUploads = 1000)
        => new(new FileSystemObjectStorageOptions { RootPath = _root.Path, MultipartExpiry = TimeSpan.FromHours(24), MaxMultipartUploads = maxUploads }, _clock, NullLoggerFactory.Instance);

    /// <summary>
    /// 时刻固定、取时刻时先执行一个动作的时钟
    /// </summary>
    private sealed class CallbackClock(DateTimeOffset now) : TimeProvider
    {
        /// <summary>
        /// 取时刻之前执行的动作
        /// </summary>
        public Action? OnRead { get; set; }

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow()
        {
            OnRead?.Invoke();
            return now;
        }
    }
}

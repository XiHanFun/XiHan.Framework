// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 分片暂存的上限（ADR-0184）
/// </summary>
/// <remarks>
/// 暂存的分片计入总配额，未完成的上传数有上限，过期的上传拒绝再传分片与完成，存储运行期间清掉过期的上传、释放它们占的配额
/// </remarks>
public sealed class FileSystemMultipartLimitTests : IDisposable
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
    /// 可拨动的时钟
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task StagedParts_CountTowardsTheTotalQuota()
    {
        var store = Store(maxTotal: 1024 * 1024);
        var upload = (await store.StartAsync(_container, new BlobKey("big"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        (await store.UploadPartAsync(upload, 1, new MemoryStream(new byte[600 * 1024]), CancellationToken.None)).ValueOrThrow();
        var second = await store.UploadPartAsync(upload, 2, new MemoryStream(new byte[600 * 1024]), CancellationToken.None);
        var put = await store.PutAsync(_container, new BlobKey("other"), new byte[600 * 1024], BlobPutOptions.Default, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.QuotaExceeded, second.Error?.Code);
        Assert.Equal(ObjectStorageErrors.QuotaExceeded, put.Error?.Code);
        Assert.Equal(600 * 1024, store.UsedBytes);
    }

    [Fact]
    public async Task ReuploadingAPart_CountsOnlyTheLatestVersion()
    {
        var store = Store();
        var upload = (await store.StartAsync(_container, new BlobKey("again"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        (await store.UploadPartAsync(upload, 1, new MemoryStream(new byte[300 * 1024]), CancellationToken.None)).ValueOrThrow();
        (await store.UploadPartAsync(upload, 1, new MemoryStream(new byte[100 * 1024]), CancellationToken.None)).ValueOrThrow();

        Assert.Equal(100 * 1024, store.UsedBytes);
    }

    [Fact]
    public async Task AbortingAnUpload_FreesItsStagedBytes()
    {
        var store = Store();
        var upload = (await store.StartAsync(_container, new BlobKey("gone"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        (await store.UploadPartAsync(upload, 1, new MemoryStream(new byte[300 * 1024]), CancellationToken.None)).ValueOrThrow();

        Assert.True((await store.AbortAsync(upload, CancellationToken.None)).IsSuccess);

        Assert.Equal(0, store.UsedBytes);
    }

    [Fact]
    public async Task CompletingAnUpload_CountsOnlyTheObject()
    {
        var store = Store();
        var upload = (await store.StartAsync(_container, new BlobKey("done"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var part = (await store.UploadPartAsync(upload, 1, new MemoryStream(new byte[300 * 1024]), CancellationToken.None)).ValueOrThrow();

        (await store.CompleteAsync(upload, [part], CancellationToken.None)).ValueOrThrow();

        Assert.Equal(300 * 1024, store.UsedBytes);
    }

    [Fact]
    public async Task ExpiredUpload_RejectsPartsAndCompletion()
    {
        var store = Store();
        var upload = (await store.StartAsync(_container, new BlobKey("late"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var part = (await store.UploadPartAsync(upload, 1, new MemoryStream(new byte[1024]), CancellationToken.None)).ValueOrThrow();

        _clock.Advance(TimeSpan.FromHours(25));
        var uploaded = await store.UploadPartAsync(upload, 2, new MemoryStream(new byte[1024]), CancellationToken.None);
        var completed = await store.CompleteAsync(upload, [part], CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.UploadNotFound, uploaded.Error?.Code);
        Assert.Equal(ObjectStorageErrors.UploadNotFound, completed.Error?.Code);
        Assert.Equal(0, store.UsedBytes);
    }

    [Fact]
    public async Task ExpiredUploads_AreRemovedWhileTheStoreRuns()
    {
        var store = Store();
        var stale = (await store.StartAsync(_container, new BlobKey("stale"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        (await store.UploadPartAsync(stale, 1, new MemoryStream(new byte[200 * 1024]), CancellationToken.None)).ValueOrThrow();

        _clock.Advance(TimeSpan.FromHours(25));
        (await store.StartAsync(_container, new BlobKey("fresh"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        Assert.False(Directory.Exists(Path.Combine(_root.Path, _container.Value, "uploads", stale.UploadId)));
        Assert.Equal(0, store.UsedBytes);
    }

    [Fact]
    public async Task UnfinishedUploads_AreBounded()
    {
        var store = Store(maxUploads: 3);
        for (var i = 0; i < 3; i++)
        {
            (await store.StartAsync(_container, new BlobKey($"u{i}"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        }

        var fourth = await store.StartAsync(_container, new BlobKey("u3"), BlobPutOptions.Default, CancellationToken.None);

        Assert.Equal((ObjectStorageErrors.QuotaExceeded, ObjectStorageMessages.UploadsExhausted), (fourth.Error?.Code, fourth.Error?.MessageKey));
        Assert.Equal(3, Directory.EnumerateDirectories(Path.Combine(_root.Path, _container.Value, "uploads")).Count());
    }

    [Fact]
    public async Task FinishedUploads_FreeTheirSlot()
    {
        var store = Store(maxUploads: 1);
        var first = (await store.StartAsync(_container, new BlobKey("a"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        Assert.True((await store.AbortAsync(first, CancellationToken.None)).IsSuccess);

        var second = (await store.StartAsync(_container, new BlobKey("b"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var part = (await store.UploadPartAsync(second, 1, new MemoryStream(new byte[10]), CancellationToken.None)).ValueOrThrow();
        (await store.CompleteAsync(second, [part], CancellationToken.None)).ValueOrThrow();

        Assert.True((await store.StartAsync(_container, new BlobKey("c"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task StartupScan_CountsStagedPartsAndOpenUploads()
    {
        var first = Store(maxUploads: 1);
        var upload = (await first.StartAsync(_container, new BlobKey("kept"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        (await first.UploadPartAsync(upload, 1, new MemoryStream(new byte[300 * 1024]), CancellationToken.None)).ValueOrThrow();

        var second = Store(maxUploads: 1);
        await second.InitializeAsync(CancellationToken.None);

        Assert.Equal(300 * 1024, second.UsedBytes);
        Assert.Equal(ObjectStorageMessages.UploadsExhausted, (await second.StartAsync(_container, new BlobKey("next"), BlobPutOptions.Default, CancellationToken.None)).Error?.MessageKey);
    }

    /// <summary>
    /// 以给定上限创建存储（分片上传保留 24 小时）
    /// </summary>
    private FileSystemBlobStore Store(long maxTotal = 1L << 30, int maxUploads = 1000)
        => new(new FileSystemObjectStorageOptions { RootPath = _root.Path, MaxTotalBytes = maxTotal, MaxBlobBytes = maxTotal, MultipartExpiry = TimeSpan.FromHours(24), MaxMultipartUploads = maxUploads }, _clock, NullLoggerFactory.Instance);
}

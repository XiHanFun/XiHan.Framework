// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 文件系统实现特有的行为：目录布局与原子写入、配额、启动扫描与临时文件清理、选项解析、宿主装配。
/// </summary>
public sealed class FileSystemBlobStoreTests : IDisposable
{
    /// <summary>
    /// 本用例的临时根目录。
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <summary>
    /// 固定时钟。
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    /// <summary>
    /// 以给定配额创建存储。
    /// </summary>
    private FileSystemBlobStore Store(long maxTotal = 1L << 30, long maxBlob = 256L << 20)
        => new(new FileSystemObjectStorageOptions { RootPath = _root.Path, MaxTotalBytes = maxTotal, MaxBlobBytes = maxBlob }, _clock);

    /// <summary>
    /// UTF-8 字节。
    /// </summary>
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public async Task Layout_PutsContentAndMetadataUnderTheContainer_AndStampsTheClock()
    {
        var store = Store();
        var written = (await store.PutAsync("docs", "2026/a.txt", Bytes("hello"), new BlobPutOptions { ContentType = "text/plain", Metadata = new Dictionary<string, string> { ["owner"] = "alice" } }, CancellationToken.None)).ValueOrThrow();

        Assert.True(File.Exists(Path.Combine(_root.Path, "docs", "data", "2026", "a.txt")));
        Assert.True(File.Exists(Path.Combine(_root.Path, "docs", "meta", "2026", "a.txt.json")));
        Assert.Equal(_clock.GetUtcNow(), written.LastModified);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", written.ETag);
        Assert.Equal(5, store.UsedBytes);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root.Path, "docs", "data"), "*.xihan-tmp-*", SearchOption.AllDirectories));

        // 覆盖后用量按差额更新；删除后归零
        Assert.True((await store.PutAsync("docs", "2026/a.txt", Bytes("hello world"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal(11, store.UsedBytes);
        Assert.True((await store.DeleteAsync("docs", "2026/a.txt", CancellationToken.None)).ValueOrThrow());
        Assert.Equal(0, store.UsedBytes);
        Assert.False(File.Exists(Path.Combine(_root.Path, "docs", "meta", "2026", "a.txt.json")));
    }

    [Fact]
    public async Task Quota_RejectsOversizedBlobs_AndTotalOverflow_WithoutLeavingContent()
    {
        var store = Store(maxTotal: 10, maxBlob: 6);

        var tooBig = await store.PutAsync("quota", "big", Bytes("1234567"), BlobPutOptions.Default, CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.QuotaExceeded, tooBig.Error?.Code);
        Assert.False(File.Exists(Path.Combine(_root.Path, "quota", "data", "big")));
        Assert.Equal(0, store.UsedBytes);

        Assert.True((await store.PutAsync("quota", "a", Bytes("123456"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        var overflow = await store.PutAsync("quota", "b", Bytes("12345"), BlobPutOptions.Default, CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.QuotaExceeded, overflow.Error?.Code);
        Assert.Equal(6, store.UsedBytes);

        // 覆盖只按差额计：把 a 换成 4 字节后，再写 5 字节就能放下
        Assert.True((await store.PutAsync("quota", "a", Bytes("1234"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.True((await store.PutAsync("quota", "b", Bytes("12345"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal(9, store.UsedBytes);
        Assert.Empty(Directory.EnumerateFiles(_root.Path, "*.xihan-tmp-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Initialize_ScansUsage_CleansTemporaryFiles_AndTreatsForeignFilesAsBlobs()
    {
        var first = Store();
        Assert.True((await first.PutAsync("ctr", "x/y", Bytes("12345"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        var data = Path.Combine(_root.Path, "ctr", "data");
        await File.WriteAllBytesAsync(Path.Combine(data, "foreign.bin"), new byte[7]);
        await File.WriteAllBytesAsync(Path.Combine(data, "x", "y.xihan-tmp-dead"), new byte[100]);

        var second = Store();
        Assert.Equal(0, second.UsedBytes);
        await second.InitializeAsync(CancellationToken.None);
        Assert.Equal(12, second.UsedBytes);
        Assert.False(File.Exists(Path.Combine(data, "x", "y.xihan-tmp-dead")));

        // 没有元数据文件的外部文件按文件信息补元数据，列举时可见
        var foreign = (await second.HeadAsync("ctr", "foreign.bin", CancellationToken.None)).ValueOrThrow();
        Assert.Equal((7L, BlobPutOptions.DefaultContentType), (foreign.Length, foreign.ContentType));
        Assert.Equal(["foreign.bin", "x/y"], (await second.ListAsync("ctr", BlobQuery.All, CancellationToken.None)).ValueOrThrow().Items.Select(static m => m.Key.Value));
    }

    [Fact]
    public async Task PathEncoding_KeepsKeysCaseSensitive_AndRoundTripsSpecialSegments()
    {
        Assert.Equal("%46ile", BlobPathEncoding.EncodeSegment("File"));
        Assert.Equal("file", BlobPathEncoding.EncodeSegment("file"));
        Assert.Equal("%63on", BlobPathEncoding.EncodeSegment("con"));
        Assert.Equal("a%2E", BlobPathEncoding.EncodeSegment("a."));
        Assert.Equal("%E4%B8%AD%25x%3A", BlobPathEncoding.EncodeSegment("中%x:"));
        foreach (var segment in new[] { "File", "con", "a.", "中%x:", "plain-1_2.txt", "LPT1.txt" })
        {
            Assert.Equal(segment, BlobPathEncoding.DecodeSegment(BlobPathEncoding.EncodeSegment(segment)));
        }

        var store = Store();
        Assert.True((await store.PutAsync("enc", "Dir/File", Bytes("upper"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.True((await store.PutAsync("enc", "dir/file", Bytes("lower"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.True((await store.PutAsync("enc", "中文/文件.txt", Bytes("cjk"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal("upper", Encoding.UTF8.GetString((await store.ReadAllBytesAsync("enc", "Dir/File", CancellationToken.None)).ValueOrThrow()));
        Assert.Equal("lower", Encoding.UTF8.GetString((await store.ReadAllBytesAsync("enc", "dir/file", CancellationToken.None)).ValueOrThrow()));
        Assert.Equal(["Dir/File", "dir/file", "中文/文件.txt"], (await store.ListAsync("enc", BlobQuery.All, CancellationToken.None)).ValueOrThrow().Items.Select(static m => m.Key.Value));
        Assert.True(File.Exists(Path.Combine(_root.Path, "enc", "data", "%44ir", "%46ile")));
    }

    [Fact]
    public void Options_ValidateAndParse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => new FileSystemObjectStorageOptions { MaxTotalBytes = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new FileSystemObjectStorageOptions { MaxBlobBytes = -1 });
        Assert.Throws<ArgumentException>(static () => new FileSystemObjectStorageOptions { RootPath = " " });
        Assert.EndsWith("blobs", new FileSystemObjectStorageOptions().RootPath, StringComparison.Ordinal);

        var options = FileSystemObjectStorageOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:ObjectStorage:FileSystem:RootPath"] = _root.Path,
            ["XiHan:ObjectStorage:FileSystem:MaxTotalBytes"] = "2048",
            ["XiHan:ObjectStorage:FileSystem:MaxBlobBytes"] = "512",
        }).Build());
        Assert.Equal((_root.Path, 2048L, 512L), (options.RootPath, options.MaxTotalBytes, options.MaxBlobBytes));
        Assert.Throws<ArgumentException>(static () => FileSystemObjectStorageOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:ObjectStorage:FileSystem:MaxTotalBytes"] = "lots" }).Build()));
    }

    [Fact]
    public async Task Feature_RegistersTheStoreAsDefault_AndInitializesOnStart()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<FileSystemObjectStorageFeature>(),
            [new("XiHan:ObjectStorage:FileSystem:RootPath", _root.Path)]);

        var store = host.Services.GetRequiredService<IBlobStore>();
        Assert.IsType<FileSystemBlobStore>(store);
        Assert.Equal(Path.GetFullPath(_root.Path), ((FileSystemBlobStore)store).RootPath);
        Assert.True(Directory.Exists(_root.Path));
        Assert.True((await store.PutAsync("hosted", "k", Bytes("v"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.Equal("v", Encoding.UTF8.GetString((await store.ReadAllBytesAsync("hosted", "k", CancellationToken.None)).ValueOrThrow()));
        Assert.Equal(["objectstorage.blob", "objectstorage.multipart"], host.App.Graph["xihan.objectstorage.filesystem"].SelectedCapabilities);
        Assert.Same(store, host.Services.GetRequiredService<IBlobMultipartStore>());
    }

    [Fact]
    public async Task Initialize_RemovesExpiredMultipartUploads_AndKeepsRecentOnes()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new FileSystemBlobStore(new FileSystemObjectStorageOptions { RootPath = _root.Path, MultipartExpiry = TimeSpan.FromHours(24) }, clock);
        await store.InitializeAsync(CancellationToken.None);
        var stale = (await store.StartAsync("uploads", "stale.bin", BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        clock.Advance(TimeSpan.FromHours(20));
        var recent = (await store.StartAsync("uploads", "recent.bin", BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        Assert.True((await store.UploadPartAsync(recent, 1, new MemoryStream(Bytes("part")), CancellationToken.None)).IsSuccess);

        // 第一次发起后 25 小时：过期的清掉，20 小时后发起的那次还在且分片完好；分片不计入用量
        clock.Advance(TimeSpan.FromHours(5));
        await store.InitializeAsync(CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await store.ListPartsAsync(stale, CancellationToken.None)).Error?.Code);
        Assert.Single((await store.ListPartsAsync(recent, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(0, store.UsedBytes);

        // 伪造的上传标识（不是本实现发出的格式）一律当作不存在，不会拼出根目录外的路径
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await store.ListPartsAsync(new BlobMultipartUpload("uploads", "recent.bin", "../../escape"), CancellationToken.None)).Error?.Code);
    }
}

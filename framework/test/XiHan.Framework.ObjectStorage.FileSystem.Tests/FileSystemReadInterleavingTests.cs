// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 读取与覆盖、删除交错（ADR-0241）
/// </summary>
/// <remarks>
/// 正在读的对象照样能被覆盖或删除，读取方读完的是它打开的那一版；一次读取拿到的元数据与内容属于同一版；读元数据的一瞬不挡覆盖；读取期间被删的对象报不存在，不报存储不可用
/// </remarks>
public sealed class FileSystemReadInterleavingTests : IDisposable
{
    /// <summary>
    /// 容器
    /// </summary>
    private static readonly ContainerName _container = new("interleave");

    /// <summary>
    /// 本用例的临时根目录
    /// </summary>
    private readonly TemporaryRoot _root = new();

    /// <inheritdoc />
    public void Dispose() => _root.Dispose();

    [Fact]
    public async Task Overwrite_WhileTheBlobIsBeingDownloaded_ReplacesIt_AndTheDownloadFinishesTheVersionItOpened()
    {
        // 修之前 Windows 上替换不了正被打开的文件，带 FileShare.Delete 也不行：下载期间覆盖以 OBJECTSTORAGE_UNAVAILABLE_001 失败
        var store = Store();
        var key = new BlobKey("downloading.bin");
        var old = Filled(100 * 1024, (byte)'a');
        var replacement = Filled(200 * 1024, (byte)'b');
        (await store.PutAsync(_container, key, old, BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var download = (await store.GetAsync(_container, key, CancellationToken.None)).ValueOrThrow();
        await using (download)
        {
            var head = new byte[10 * 1024];
            await download.Content.ReadExactlyAsync(head, CancellationToken.None);

            var overwrite = await store.PutAsync(_container, key, replacement, BlobPutOptions.Default, CancellationToken.None);

            Assert.True(overwrite.IsSuccess, overwrite.Error?.ToString());
            var rest = await ReadToEndAsync(download.Content);
            var downloaded = head.Concat(rest).ToArray();
            Assert.Equal(old, downloaded);
            Assert.Equal(download.Metadata.Length, downloaded.Length);
            Assert.Equal(download.Metadata.ETag, Sha256(downloaded));
        }

        var now = (await store.HeadAsync(_container, key, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(replacement.Length, now.Length);
        Assert.Equal(Sha256(replacement), now.ETag);
        Assert.Equal(replacement, (await store.ReadAllBytesAsync(_container, key, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(replacement.Length, store.UsedBytes);
    }

    [Fact]
    public async Task Delete_WhileTheBlobIsBeingDownloaded_RemovesIt_AndTheKeyCanBeWrittenAgain()
    {
        var store = Store();
        var key = new BlobKey("deleting.bin");
        var old = Filled(64 * 1024, (byte)'a');
        (await store.PutAsync(_container, key, old, BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var download = (await store.GetAsync(_container, key, CancellationToken.None)).ValueOrThrow();
        await using (download)
        {
            Assert.True((await store.DeleteAsync(_container, key, CancellationToken.None)).ValueOrThrow());
            Assert.Equal(ObjectStorageErrors.BlobNotFound, (await store.HeadAsync(_container, key, CancellationToken.None)).Error?.Code);

            // 删掉之后同一个键立刻能以不覆盖的方式重新写入，不等下载关掉旧内容
            var again = await store.PutAsync(_container, key, Filled(32 * 1024, (byte)'b'), new BlobPutOptions { Overwrite = false }, CancellationToken.None);

            Assert.True(again.IsSuccess, again.Error?.ToString());
            Assert.Equal(old, await ReadToEndAsync(download.Content));
        }

        Assert.Equal(32 * 1024, (await store.HeadAsync(_container, key, CancellationToken.None)).ValueOrThrow().Length);
        Assert.Equal(32 * 1024, store.UsedBytes);
    }

    [Fact]
    public async Task Overwrite_WhileAnotherReaderHoldsTheMetadataOpen_ReplacesContentAndMetadataTogether()
    {
        // 另一个读取方（比如共享目录的另一个进程）正在读元数据的那一瞬，以容许读与删除共享的句柄打开着它。
        // 修之前 Windows 上内容已经换上、元数据替换以拒绝访问失败：调用方拿到 OBJECTSTORAGE_UNAVAILABLE_001，盘上是新内容配旧的长度与 ETag
        var store = Store();
        var key = new BlobKey("meta-held.bin");
        (await store.PutAsync(_container, key, Filled(10 * 1024, (byte)'a'), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var replacement = Filled(20 * 1024, (byte)'b');

        var metaPath = Path.Combine(_root.Path, _container.Value, "meta", "meta-held.bin.json");
        using (new FileStream(metaPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            var overwrite = await store.PutAsync(_container, key, replacement, BlobPutOptions.Default, CancellationToken.None);

            Assert.True(overwrite.IsSuccess, overwrite.Error?.ToString());
        }

        var now = (await store.GetAsync(_container, key, CancellationToken.None)).ValueOrThrow();
        await using (now)
        {
            Assert.Equal(replacement.Length, now.Metadata.Length);
            Assert.Equal(Sha256(replacement), now.Metadata.ETag);
            Assert.Equal(replacement, await ReadToEndAsync(now.Content));
        }
    }

    [Fact]
    public async Task Get_WhileTheKeyIsOverwritten_ReturnsTheMetadataOfTheContentItOpened()
    {
        // 修之前读取先读元数据、再打开内容，两步之间被覆盖时元数据是上一版的长度与 ETag、内容是下一版；
        // Windows 上覆盖还会撞上正被读取的内容或元数据，以 OBJECTSTORAGE_UNAVAILABLE_001 失败。
        // 两步之间没有能让读取方停住的位置，靠轮数：对象小、提交快、8 个读取方。修之前本机连跑 6 次都失败；
        // 只换成 POSIX 语义的改名、读取不核对变更代数时，8 次里 6 次碰上错配
        var store = Store();
        var key = new BlobKey("flipping.bin");
        var versions = new[] { Filled(4 * 1024, (byte)'a'), Filled(6 * 1024, (byte)'b') };
        var etags = versions.Select(Sha256).ToArray();
        (await store.PutAsync(_container, key, versions[0], BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var writing = Task.Run(async () =>
        {
            var failures = new List<string>();
            for (var i = 1; i <= OverwriteRounds; i++)
            {
                var written = await store.PutAsync(_container, key, versions[i % 2], BlobPutOptions.Default, CancellationToken.None);
                if (written.IsFailure)
                {
                    failures.Add(written.Error!.ToString());
                }
            }

            return failures;
        });
        var readers = Enumerable.Range(0, 8).Select(reader => Task.Run(async () =>
        {
            var mismatches = new List<string>();
            var reads = 0;
            while (!writing.IsCompleted)
            {
                // 整个对象与范围读取轮流：两种读取都要拿到同一版的元数据与内容
                var ranged = reads++ % 2 == 1;
                var opened = ranged
                    ? await store.GetAsync(_container, key, new BlobRange(1000, 2048), CancellationToken.None)
                    : await store.GetAsync(_container, key, CancellationToken.None);
                if (opened.IsFailure)
                {
                    mismatches.Add($"read failed: {opened.Error}");
                    continue;
                }

                var content = opened.ValueOrThrow();
                await using (content)
                {
                    var bytes = await ReadToEndAsync(content.Content);
                    var version = Array.IndexOf(etags, content.Metadata.ETag);
                    var expected = version < 0 ? null : ranged ? versions[version].AsSpan(1000, 2048).ToArray() : versions[version];
                    if (expected is null || content.Metadata.Length != versions[version].Length || !bytes.AsSpan().SequenceEqual(expected))
                    {
                        mismatches.Add($"metadata length {content.Metadata.Length} with {bytes.Length} bytes of '{(bytes.Length > 0 ? (char)bytes[0] : '-')}' (ranged: {ranged})");
                    }
                }
            }

            return (reads, mismatches);
        })).ToList();

        var failures = await writing;
        var observed = await Task.WhenAll(readers);

        Assert.Empty(failures);
        Assert.Empty(observed.SelectMany(static r => r.mismatches));
        Assert.All(observed, static r => Assert.True(r.reads > 0, "Every reader must have read at least once."));
    }

    [Fact]
    public async Task HeadAndList_WhileTheKeyIsDeletedAndWrittenAgain_ReportItOrNothing()
    {
        // 修之前读元数据先看内容在不在、再打开元数据：两步之间被删时以 FileNotFoundException 落到 OBJECTSTORAGE_UNAVAILABLE_001，
        // 列举整页失败，而它应当跳过这个键。窗口只有一次 stat 那么宽，这里只是守住修正：修之前本机这 300 轮连跑 6 次都没碰上，
        // 3000 轮的诊断里两次分别有 56 与 25 次 Head、36 与 22 次列举失败
        var store = Store();
        var key = new BlobKey("blinking.bin");
        var content = Filled(4 * 1024, (byte)'a');

        var churning = Task.Run(async () =>
        {
            var failures = new List<string>();
            for (var i = 0; i < ChurnRounds; i++)
            {
                var written = await store.PutAsync(_container, key, content, BlobPutOptions.Default, CancellationToken.None);
                var deleted = await store.DeleteAsync(_container, key, CancellationToken.None);
                if (written.IsFailure || deleted.IsFailure)
                {
                    failures.Add((written.Error ?? deleted.Error)!.ToString());
                }
            }

            return failures;
        });
        var probers = Enumerable.Range(0, 4).Select(prober => Task.Run(async () =>
        {
            var unexpected = new List<string>();
            var reads = 0;
            while (!churning.IsCompleted)
            {
                reads++;
                var head = await store.HeadAsync(_container, key, CancellationToken.None);
                if (head.IsFailure && head.Error!.Code != ObjectStorageErrors.BlobNotFound)
                {
                    unexpected.Add($"head: {head.Error}");
                }
                else if (head.IsSuccess && head.ValueOrThrow().Length != content.Length)
                {
                    unexpected.Add($"head length {head.ValueOrThrow().Length}");
                }

                var page = await store.ListAsync(_container, BlobQuery.All, CancellationToken.None);
                if (page.IsFailure)
                {
                    unexpected.Add($"list: {page.Error}");
                }
            }

            return (reads, unexpected);
        })).ToList();

        var failures = await churning;
        var observed = await Task.WhenAll(probers);

        Assert.Empty(failures);
        Assert.Empty(observed.SelectMany(static r => r.unexpected));
        Assert.All(observed, static r => Assert.True(r.reads > 0, "Every prober must have probed at least once."));
    }

    /// <summary>
    /// 读取期间覆盖的轮数
    /// </summary>
    private const int OverwriteRounds = 600;

    /// <summary>
    /// 探测期间写入又删除的轮数
    /// </summary>
    private const int ChurnRounds = 300;

    /// <summary>
    /// 以足够大的配额创建存储
    /// </summary>
    private FileSystemBlobStore Store()
        => new(new FileSystemObjectStorageOptions { RootPath = _root.Path, MaxTotalBytes = 1L << 30, MaxBlobBytes = 1L << 30 }, new FakeTimeProvider(), NullLoggerFactory.Instance);

    /// <summary>
    /// 全是同一个字节的内容
    /// </summary>
    private static byte[] Filled(int length, byte value)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    /// <summary>
    /// 内容的 SHA-256（小写十六进制，与 ETag 相同）
    /// </summary>
    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// 读完流里剩下的字节
    /// </summary>
    private static async Task<byte[]> ReadToEndAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, CancellationToken.None);
        return buffer.ToArray();
    }
}

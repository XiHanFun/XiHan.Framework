// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.ObjectStorage.Conformance;

/// <summary>
/// <see cref="IBlobStore"/> 的一致性套件。继承并实现 <see cref="CreateStore"/>，全部用例随之执行。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class BlobStoreConformance
{
    /// <summary>
    /// 用例使用的容器。
    /// </summary>
    protected virtual ContainerName Container => new("conformance");

    /// <summary>
    /// 创建被测存储。每个用例调用一次；返回的存储在 <see cref="Container"/> 里不应含有本套件使用的键。
    /// </summary>
    protected abstract IBlobStore CreateStore();

    /// <summary>
    /// 用例 1：写入后能读到同样的字节、内容类型与自定义元数据；ETag 与长度正确；没写过的键是 NOTFOUND。
    /// </summary>
    [Fact]
    public async Task Put_ThenGet_RoundTripsContentAndMetadata()
    {
        var store = CreateStore();
        var key = Key("a/b/file.txt");
        var options = new BlobPutOptions { ContentType = "text/plain; charset=utf-8", Metadata = new Dictionary<string, string> { ["owner"] = "alice", ["x-trace"] = "t-1" } };

        var written = (await store.PutAsync(Container, key, Bytes("hello"), options, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(key, written.Key);
        Assert.Equal(5, written.Length);
        Assert.Equal("text/plain; charset=utf-8", written.ContentType);
        Assert.False(string.IsNullOrWhiteSpace(written.ETag));
        Assert.Equal("alice", written.Metadata["owner"]);

        var got = (await store.GetAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        await using (got)
        {
            Assert.Equal(written with { LastModified = got.Metadata.LastModified }, got.Metadata with { Metadata = written.Metadata });
            Assert.Equal("t-1", got.Metadata.Metadata["x-trace"]);
            Assert.Equal("hello", await Text(got.Content).ConfigureAwait(false));
        }

        var head = (await store.HeadAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((written.Length, written.ETag, written.ContentType), (head.Length, head.ETag, head.ContentType));

        var missing = await store.GetAsync(Container, Key("missing"), CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(ObjectStorageErrors.BlobNotFound, missing.Error?.Code);
        Assert.Equal(ObjectStorageErrors.BlobNotFound, (await store.HeadAsync(Container, Key("missing"), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.False((await store.ExistsAsync(Container, Key("missing"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 2：允许覆盖时新内容替换旧内容且 ETag 变化；不允许覆盖时返回 CONFLICT 且内容不变；键区分大小写。
    /// </summary>
    [Fact]
    public async Task Put_OverwritesOrConflicts_AndKeysAreCaseSensitive()
    {
        var store = CreateStore();
        var root = Guid.NewGuid().ToString("N")[..8];
        var key = new BlobKey(root + "/case/File");
        var lower = new BlobKey(root + "/case/file");

        var first = (await store.PutAsync(Container, key, Bytes("v1"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var second = (await store.PutAsync(Container, key, Bytes("v2-longer"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.NotEqual(first.ETag, second.ETag);
        Assert.Equal("v2-longer", Text((await store.ReadAllBytesAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));

        var refused = await store.PutAsync(Container, key, Bytes("v3"), new BlobPutOptions { Overwrite = false }, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(ObjectStorageErrors.BlobExists, refused.Error?.Code);
        Assert.Equal("v2-longer", Text((await store.ReadAllBytesAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));

        Assert.True((await store.PutAsync(Container, lower, Bytes("lower"), new BlobPutOptions { Overwrite = false }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal("lower", Text((await store.ReadAllBytesAsync(Container, lower, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));
        Assert.Equal("v2-longer", Text((await store.ReadAllBytesAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));
    }

    /// <summary>
    /// 用例 3：批量写入按顺序逐项返回结果，空列表返回空；重复键按顺序各执行一次。
    /// </summary>
    [Fact]
    public async Task PutMany_ReturnsOneResultPerUpload_InOrder()
    {
        var store = CreateStore();
        var key = Key("batch/dup");
        var other = Key("batch/other");
        using var a = Bytes("a");
        using var b = Bytes("b");
        using var c = Bytes("c");

        var results = await store.PutAsync(Container,
        [
            new BlobUpload(key, a, BlobPutOptions.Default),
            new BlobUpload(other, b, BlobPutOptions.Default),
            new BlobUpload(key, c, new BlobPutOptions { Overwrite = false }),
        ], CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(3, results.Count);
        Assert.Equal([key, other, key], results.Select(static r => r.Key));
        Assert.True(results[0].Outcome.IsSuccess);
        Assert.True(results[1].Outcome.IsSuccess);
        Assert.Equal(ObjectStorageErrors.BlobExists, results[2].Outcome.Error?.Code);
        Assert.Equal("a", Text((await store.ReadAllBytesAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));
        Assert.Empty(await store.PutAsync(Container, [], CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 4：批量删除逐项报告是否存在；不存在是成功；重复键第二次报告不存在；删除后读不到。
    /// </summary>
    [Fact]
    public async Task DeleteMany_ReportsExistencePerKey()
    {
        var store = CreateStore();
        var one = Key("del/one");
        var two = Key("del/two");
        var missing = Key("del/missing");
        Assert.True((await store.PutAsync(Container, one, Bytes("1"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await store.PutAsync(Container, two, Bytes("2"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        var results = await store.DeleteAsync(Container, [one, missing, one, two], CancellationToken.None).ConfigureAwait(false);
        Assert.Equal([true, false, false, true], results.Select(static r => r.Outcome.ValueOrThrow()));
        Assert.Equal([one, missing, one, two], results.Select(static r => r.Key));
        Assert.Equal(ObjectStorageErrors.BlobNotFound, (await store.HeadAsync(Container, one, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.False((await store.DeleteAsync(Container, two, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty(await store.DeleteAsync(Container, [], CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 5：按前缀列举，键的序数顺序；页大小生效，续页标记取完全部且不重复；无匹配为空页。
    /// </summary>
    [Fact]
    public async Task List_FiltersByPrefix_OrdersByKey_AndPaginates()
    {
        var store = CreateStore();
        var root = Guid.NewGuid().ToString("N")[..8];
        foreach (var key in new[] { "list/b", "list/a/2", "list/a/1", "list/c", "other/x" })
        {
            Assert.True((await store.PutAsync(Container, new BlobKey(root + "/" + key), Bytes(key), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        string[] expected = [root + "/list/a/1", root + "/list/a/2", root + "/list/b", root + "/list/c"];
        var all = (await store.ListAsync(Container, new BlobQuery { Prefix = root + "/list/" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(expected, all.Items.Select(static m => m.Key.Value));
        Assert.Null(all.ContinuationToken);
        Assert.All(all.Items, static m => Assert.True(m.Length > 0 && m.ETag.Length > 0));

        var collected = new List<string>();
        string? token = null;
        var pages = 0;
        do
        {
            var page = (await store.ListAsync(Container, new BlobQuery { Prefix = root + "/list/", PageSize = 3, ContinuationToken = token }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            collected.AddRange(page.Items.Select(static m => m.Key.Value));
            token = page.ContinuationToken;
            pages++;
        }
        while (token is not null);

        Assert.Equal(expected, collected);
        Assert.Equal(2, pages);
        Assert.Equal([root + "/list/a/1", root + "/list/a/2"], (await store.ListAsync(Container, new BlobQuery { Prefix = root + "/list/a/" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items.Select(static m => m.Key.Value));
        Assert.Empty((await store.ListAsync(Container, new BlobQuery { Prefix = root + "/nothing/" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items);
    }

    /// <summary>
    /// 用例 6：大对象流式写入与读出内容一致，不要求一次装进内存。
    /// </summary>
    [Fact]
    public async Task LargeContent_StreamsThrough()
    {
        var store = CreateStore();
        var key = Key("large/blob.bin");
        var payload = new byte[3 * 1024 * 1024 + 17];
        RandomNumberGenerator.Fill(payload);

        using (var source = new MemoryStream(payload, writable: false))
        {
            var written = (await store.PutAsync(Container, key, source, BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal(payload.Length, written.Length);
        }

        var got = (await store.GetAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        await using (got)
        {
            using var sink = new MemoryStream();
            await got.Content.CopyToAsync(sink, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(payload, sink.ToArray());
        }
    }

    /// <summary>
    /// 用例 7：不合法的容器名、键、元数据名与查询在构造时拒绝。
    /// </summary>
    [Fact]
    public void InvalidIdentifiers_AreRejectedEarly()
    {
        Assert.Throws<ArgumentException>(static () => new ContainerName("Upper"));
        Assert.Throws<ArgumentException>(static () => new ContainerName("ab"));
        Assert.Throws<ArgumentException>(static () => new ContainerName("-abc"));
        Assert.Throws<ArgumentException>(static () => new BlobKey(string.Empty));
        Assert.Throws<ArgumentException>(static () => new BlobKey("/leading"));
        Assert.Throws<ArgumentException>(static () => new BlobKey("trailing/"));
        Assert.Throws<ArgumentException>(static () => new BlobKey("a//b"));
        Assert.Throws<ArgumentException>(static () => new BlobKey("a/../b"));
        Assert.Throws<ArgumentException>(static () => new BlobKey("a\\b"));
        Assert.Throws<ArgumentException>(static () => new BlobKey("a b"));
        Assert.Throws<ArgumentException>(static () => new BlobPutOptions { Metadata = new Dictionary<string, string> { ["Bad Name"] = "x" } });
        Assert.Throws<ArgumentException>(static () => new BlobPutOptions { ContentType = " " });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new BlobQuery { PageSize = 0 });
        Assert.Throws<ArgumentException>(static () => new BlobQuery { Prefix = "a b" });
        Assert.True(BlobKey.IsValid("a/b.c-d_e"));
        Assert.True(BlobKey.IsValidPrefix(string.Empty));
        Assert.True(BlobKey.IsValidPrefix("a/"));
    }

    /// <summary>
    /// 用例 8：范围读取——起点与长度、读到末尾、超出末尾截断，<see cref="BlobContent.Range"/> 给出实际范围，元数据长度仍是总长；起点不在对象内（含空对象）不可满足；不存在是 NOTFOUND。
    /// </summary>
    [Fact]
    public async Task GetRange_ReturnsTheRequestedBytes()
    {
        var store = CreateStore();
        var key = Key("range/digits");
        Assert.True((await store.PutAsync(Container, key, Bytes("0123456789"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        foreach (var (range, expected, offset, length) in new[] { (new BlobRange(2, 3), "234", 2L, 3L), (new BlobRange(7), "789", 7L, 3L), (new BlobRange(8, 100), "89", 8L, 2L), (new BlobRange(0), "0123456789", 0L, 10L) })
        {
            var got = (await store.GetAsync(Container, key, range, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            await using (got)
            {
                Assert.Equal(expected, await Text(got.Content).ConfigureAwait(false));
                Assert.Equal(new BlobContentRange(offset, length, 10), got.Range);
                Assert.Equal(10, got.Metadata.Length);
            }
        }

        var beyond = await store.GetAsync(Container, key, new BlobRange(10), CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(ObjectStorageErrors.RangeNotSatisfiable, beyond.Error?.Code);
        Assert.Equal(10L, Convert.ToInt64(beyond.Error!.Details!["length"], System.Globalization.CultureInfo.InvariantCulture));

        var empty = Key("range/empty");
        Assert.True((await store.PutAsync(Container, empty, Bytes(string.Empty), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(ObjectStorageErrors.RangeNotSatisfiable, (await store.GetAsync(Container, empty, new BlobRange(0), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(ObjectStorageErrors.BlobNotFound, (await store.GetAsync(Container, Key("range/missing"), new BlobRange(0), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 9：分片上传——乱序上传、同一编号重传覆盖，列出分片升序；完成前对象不可见，完成后是按编号拼接的内容，内容类型与自定义元数据来自发起时；完成后上传不存在。存储不支持分片时跳过。
    /// </summary>
    [Fact]
    public async Task Multipart_UploadsPartsInAnyOrder_AndCompletesAtomically()
    {
        var (store, multipart) = CreateMultipartStore();
        var key = Key("multipart/video.bin");
        var options = new BlobPutOptions { ContentType = "application/x-conformance", Metadata = new Dictionary<string, string> { ["owner"] = "alice" } };
        var upload = (await multipart.StartAsync(Container, key, options, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((Container, key), (upload.Container, upload.Key));

        var head = new byte[BlobMultipart.MinPartBytes];
        RandomNumberGenerator.Fill(head);
        var second = (await multipart.UploadPartAsync(upload, 2, Bytes("tail"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var first = (await multipart.UploadPartAsync(upload, 1, new MemoryStream(head, writable: false), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var resent = (await multipart.UploadPartAsync(upload, 2, Bytes("tail-2"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.NotEqual(second.ETag, resent.ETag);

        var listed = (await multipart.ListPartsAsync(upload, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([(1, first.ETag, (long)head.Length), (2, resent.ETag, 6L)], listed.Select(static p => (p.PartNumber, p.ETag, p.Length)));
        Assert.Equal(ObjectStorageErrors.BlobNotFound, (await store.HeadAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).Error?.Code);

        var completed = (await multipart.CompleteAsync(upload, listed, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((head.Length + 6L, "application/x-conformance", "alice"), (completed.Length, completed.ContentType, completed.Metadata["owner"]));
        byte[] expected = [.. head, .. Encoding.UTF8.GetBytes("tail-2")];
        Assert.Equal(expected, (await store.ReadAllBytesAsync(Container, key, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await multipart.ListPartsAsync(upload, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await multipart.UploadPartAsync(upload, 3, Bytes("late"), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 10：分片清单不合法（为空、乱序、ETag 不符、非末片过小）返回 PARTS_INVALID 且上传保留、之后仍可完成；不覆盖时对象已存在拒绝发起与完成；放弃后上传不存在，再放弃也成功。存储不支持分片时跳过。
    /// </summary>
    [Fact]
    public async Task Multipart_RejectsInvalidPartLists_AndHonoursOverwriteAndAbort()
    {
        var (store, multipart) = CreateMultipartStore();
        var key = Key("multipart/checked.bin");
        var upload = (await multipart.StartAsync(Container, key, new BlobPutOptions { Overwrite = false }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var small = (await multipart.UploadPartAsync(upload, 1, Bytes("small"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var last = (await multipart.UploadPartAsync(upload, 2, Bytes("last"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();

        foreach (var parts in new IReadOnlyList<BlobPart>[] { [], [last, small], [new BlobPart(1, "0123456789abcdef0123456789abcdef"), last], [small, last] })
        {
            Assert.Equal(ObjectStorageErrors.PartsInvalid, (await multipart.CompleteAsync(upload, parts, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        }

        // 上传仍在：只用一片（末片可以小于最小分片）即可完成
        Assert.Equal(5, (await multipart.CompleteAsync(upload, [small], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Length);

        // 不覆盖：已存在拒绝发起；发起后对象才出现，完成时拒绝
        Assert.Equal(ObjectStorageErrors.BlobExists, (await multipart.StartAsync(Container, key, new BlobPutOptions { Overwrite = false }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        var other = Key("multipart/raced.bin");
        var raced = (await multipart.StartAsync(Container, other, new BlobPutOptions { Overwrite = false }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var only = (await multipart.UploadPartAsync(raced, 1, Bytes("mine"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.True((await store.PutAsync(Container, other, Bytes("theirs"), BlobPutOptions.Default, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(ObjectStorageErrors.BlobExists, (await multipart.CompleteAsync(raced, [only], CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.Equal("theirs", Text((await store.ReadAllBytesAsync(Container, other, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));

        Assert.True((await multipart.AbortAsync(raced, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(ObjectStorageErrors.UploadNotFound, (await multipart.ListPartsAsync(raced, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.True((await multipart.AbortAsync(raced, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
    }

    /// <summary>
    /// 被测存储与其分片上传；存储没有实现 <see cref="IBlobMultipartStore"/> 时跳过用例。
    /// </summary>
    private (IBlobStore Store, IBlobMultipartStore Multipart) CreateMultipartStore()
    {
        var store = CreateStore();
        Assert.SkipWhen(store is not IBlobMultipartStore, $"{store.GetType().Name} does not implement IBlobMultipartStore.");
        return (store, (IBlobMultipartStore)store);
    }

    /// <summary>
    /// 套件内唯一的键：加随机后缀，避免共享存储时用例互相污染。
    /// </summary>
    private static BlobKey Key(string suffix) => new(Guid.NewGuid().ToString("N")[..8] + "/" + suffix);

    /// <summary>
    /// UTF-8 字节流。
    /// </summary>
    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text), writable: false);

    /// <summary>
    /// 读流为文本。
    /// </summary>
    private static async Task<string> Text(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 字节为文本。
    /// </summary>
    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}

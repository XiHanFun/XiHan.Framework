// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.S3.Model;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 对真实的 S3 兼容服务，自定义元数据的值原样往返（ADR-0333、ADR-0343）
/// </summary>
/// <remarks>
/// 非 ASCII、平面以外的字符、首尾的空格、以 <c>=?</c> 开头且以 <c>?=</c> 结尾而不整个是 encoded-word 的 ASCII、中间的连续空格、空串与接近 2048 字节上限的值，经写入、分片上传的发起与完成写下，经读取、范围读取与 <c>Head</c> 读回都是原值；服务里存下的头值（经 SDK 直接 <c>Head</c>）解得回原值，原样存取的服务（RustFS）与先解码再编码的服务（Amazon S3 的文档写法）都满足。整个是 encoded-word 形状的值写入之前拒收、什么都不写；别的写入方原样写进头的 UTF-8 字节读得回原值（RustFS 1.0.6 读回时照 RFC 2047 编码，ADR-0343 实测）。未设置 <c>XIHAN_TEST_S3</c> 时跳过
/// </remarks>
public sealed class S3MetadataRoundTripTests
{
    /// <summary>
    /// 用例的桶
    /// </summary>
    private const string Bucket = "xihan-metadata";

    /// <summary>
    /// 一组要往返的值
    /// </summary>
    private static Dictionary<string, string> Values() => new(StringComparer.Ordinal)
    {
        ["title"] = "2026 年度报告",
        ["emoji"] = "\U0001F600 smile",
        ["spaced"] = " leading and trailing ",
        ["word"] = "=?x?=",
        ["gaps"] = "a  b",
        ["empty"] = string.Empty,
        ["plain"] = "plain",
    };

    [Fact]
    public async Task Put_ThenGetAndHead_RoundTripsEveryValue()
    {
        var store = S3TestServer.CreateStore(Bucket);
        var key = new BlobKey(Guid.NewGuid().ToString("N") + "/report.txt");
        var values = Values();

        var written = (await store.PutAsync(new ContainerName(Bucket), key, "abc"u8.ToArray(), new BlobPutOptions { Metadata = values }, CancellationToken.None)).ValueOrThrow();
        try
        {
            Assert.Equal(values, written.Metadata);
            Assert.Equal(values, (await store.HeadAsync(new ContainerName(Bucket), key, CancellationToken.None)).ValueOrThrow().Metadata);
            var got = (await store.GetAsync(new ContainerName(Bucket), key, CancellationToken.None)).ValueOrThrow();
            await using (got)
            {
                Assert.Equal(values, got.Metadata.Metadata);
                Assert.Equal("abc", await new StreamReader(got.Content).ReadToEndAsync());
            }

            var ranged = (await store.GetAsync(new ContainerName(Bucket), key, new BlobRange(1, 1), CancellationToken.None)).ValueOrThrow();
            await using (ranged)
            {
                Assert.Equal(values, ranged.Metadata.Metadata);
            }

            // 服务存下的是哪种写法都行：直接读到的头值要解得回原值
            var raw = await S3TestServer.Client().GetObjectMetadataAsync(Bucket, key.Value);
            foreach (var (name, value) in values)
            {
                Assert.True(S3MetadataHeader.TryDecode(raw.Metadata[name], out var decoded));
                Assert.Equal(value, decoded);
            }
        }
        finally
        {
            await store.DeleteAsync(new ContainerName(Bucket), key, CancellationToken.None);
        }
    }

    [Fact]
    public async Task AValueNearTheLimit_RoundTrips()
    {
        // 名与值合计 2044 个 UTF-8 字节（上限 2048）：编码之后约 3.3 KB 的头
        var store = S3TestServer.CreateStore(Bucket);
        var key = new BlobKey(Guid.NewGuid().ToString("N") + "/long.txt");
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["long"] = string.Concat(Enumerable.Repeat("中", 680)) };

        Assert.True((await store.PutAsync(new ContainerName(Bucket), key, "x"u8.ToArray(), new BlobPutOptions { Metadata = values }, CancellationToken.None)).IsSuccess);
        try
        {
            Assert.Equal(values, (await store.HeadAsync(new ContainerName(Bucket), key, CancellationToken.None)).ValueOrThrow().Metadata);
        }
        finally
        {
            await store.DeleteAsync(new ContainerName(Bucket), key, CancellationToken.None);
        }
    }

    [Fact]
    public async Task AMultipartUpload_RoundTripsEveryValue()
    {
        var store = S3TestServer.CreateStore(Bucket);
        var key = new BlobKey(Guid.NewGuid().ToString("N") + "/parts.bin");
        var values = Values();

        var upload = (await store.StartAsync(new ContainerName(Bucket), key, new BlobPutOptions { Metadata = values }, CancellationToken.None)).ValueOrThrow();
        try
        {
            var part = (await store.UploadPartAsync(upload, 1, new MemoryStream([1, 2, 3]), CancellationToken.None)).ValueOrThrow();
            var completed = (await store.CompleteAsync(upload, [part], CancellationToken.None)).ValueOrThrow();

            Assert.Equal(values, completed.Metadata);
            Assert.Equal(values, (await store.HeadAsync(new ContainerName(Bucket), key, CancellationToken.None)).ValueOrThrow().Metadata);
        }
        finally
        {
            await store.AbortAsync([upload], CancellationToken.None);
            await store.DeleteAsync(new ContainerName(Bucket), key, CancellationToken.None);
        }
    }

    [Fact]
    public async Task AnEncodedWordShapedValue_IsRefused_AndNothingIsWritten()
    {
        var store = S3TestServer.CreateStore(Bucket);
        var key = new BlobKey(Guid.NewGuid().ToString("N") + "/shaped.txt");
        var options = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["word"] = "=?UTF-8?B?YQ==?=" } };

        var put = await store.PutAsync(new ContainerName(Bucket), key, "x"u8.ToArray(), options, CancellationToken.None);
        var started = await store.StartAsync(new ContainerName(Bucket), key, options, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.MetadataValueUnsupported, put.Error?.Code);
        Assert.Equal(ObjectStorageErrors.MetadataValueUnsupported, started.Error?.Code);
        Assert.False((await store.ExistsAsync(new ContainerName(Bucket), key, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task AValueWrittenAsRawUtf8ByAnotherClient_IsReadBack()
    {
        // 别的写入方不按 RFC 2047、把 UTF-8 字节直接写进头：服务原样返回字节或自己编成 encoded-word，都读得回原值
        var store = S3TestServer.CreateStore(Bucket);
        var key = Guid.NewGuid().ToString("N") + "/raw.txt";
        using var writer = S3ObjectStorageFeature.CreateClient(S3TestServer.Options(), new Utf8HeaderHttpClientFactory());
        var request = new PutObjectRequest { BucketName = Bucket, Key = key, ContentBody = "x" };
        request.Metadata["title"] = "2026 年度报告";
        request.Metadata["mixed"] = "说明 =?UTF-8?B?5Lit?=";
        request.Metadata["long"] = string.Concat(Enumerable.Repeat("中", 100));
        await writer.PutObjectAsync(request);
        try
        {
            var head = (await store.HeadAsync(new ContainerName(Bucket), new BlobKey(key), CancellationToken.None)).ValueOrThrow();
            Assert.Equal("2026 年度报告", head.Metadata["title"]);
            Assert.Equal("说明 =?UTF-8?B?5Lit?=", head.Metadata["mixed"]);
            Assert.Equal(string.Concat(Enumerable.Repeat("中", 100)), head.Metadata["long"]);
        }
        finally
        {
            await S3TestServer.Client().DeleteObjectAsync(Bucket, key);
        }
    }

    [Fact]
    public async Task AValueWrittenEncodedByAnotherClient_IsDecoded()
    {
        // 别的写入方自己编好的 encoded-word（Q 编码、拆在两个词里）：照 RFC 2047 解出
        var store = S3TestServer.CreateStore(Bucket);
        var key = Guid.NewGuid().ToString("N") + "/foreign.txt";
        var request = new PutObjectRequest { BucketName = Bucket, Key = key, ContentBody = "x" };
        request.Metadata["title"] = "=?UTF-8?Q?2026_=E5=B9=B4=E5=BA=A6?= =?UTF-8?B?5oql5ZGK?=";
        await S3TestServer.Client().PutObjectAsync(request);
        try
        {
            Assert.Equal("2026 年度报告", (await store.HeadAsync(new ContainerName(Bucket), new BlobKey(key), CancellationToken.None)).ValueOrThrow().Metadata["title"]);
        }
        finally
        {
            await S3TestServer.Client().DeleteObjectAsync(Bucket, key);
        }
    }
}

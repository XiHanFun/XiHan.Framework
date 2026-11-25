// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Text;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 读取、写入与 HEAD 的应答缺 ETag 或修改时间时返回错误（ADR-0313）
/// </summary>
/// <remarks>
/// <c>GetObject</c>、<c>HeadObject</c> 的应答本该带 <c>ETag</c> 与 <c>Last-Modified</c>，<c>PutObject</c> 的应答本该带 <c>ETag</c>：缺了是不合规的应答，按服务端错误返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，不再以空串与 <see cref="DateTimeOffset.MinValue"/> 填进元数据。<c>PutObject</c> 的应答本来就没有修改时间，写入结果的修改时间取本机时钟。只判存在与否的 HEAD（批量删除、不覆盖写入之前）不需要这两项。线上的用例走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>；其余用内存替身 <see cref="FakeS3Client"/>。都不连 S3 服务
/// </remarks>
public sealed class S3ObjectResponseTests
{
    /// <summary>
    /// 用例的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 用例的键
    /// </summary>
    private const string Key = "a.txt";

    /// <summary>
    /// 应答里的修改时间头
    /// </summary>
    private const string LastModifiedHeader = "Last-Modified: Thu, 26 Nov 2026 08:00:00 GMT";

    /// <summary>
    /// 用例写入与读出的内容
    /// </summary>
    private const string Content = "abc";

    /// <summary>
    /// <see cref="Content"/> 的 MD5，即单次写入的 ETag
    /// </summary>
    /// <remarks>
    /// SDK 拿 ETag 核对写入与整读的内容：不是内容的 MD5 时它自己抛出，到不了存储
    /// </remarks>
    private const string ContentETag = "900150983cd24fb0d6963f7d28e17f72";

    /// <summary>
    /// 应答里的 ETag 头
    /// </summary>
    private const string ETagHeader = "ETag: \"" + ContentETag + "\"";

    /// <summary>
    /// 应答缺的那一项，与 <c>reason</c> 里点名的字段
    /// </summary>
    /// <remarks>
    /// 另带的响应头：缺 ETag、ETag 只有引号、缺修改时间
    /// </remarks>
    public static TheoryData<string[], string> IncompleteHeaders => new()
    {
        { [LastModifiedHeader], "ETag" },
        { [LastModifiedHeader, "ETag: \"\""], "ETag" },
        { [ETagHeader], "Last-Modified" },
    };

    /// <summary>
    /// 整读的应答缺的那一项，与 <c>reason</c> 里点名的字段
    /// </summary>
    /// <remarks>
    /// 没有只有引号的 ETag：整读时 SDK 先拿 ETag 当内容的 MD5 去核对，只有引号的 ETag 它自己就抛出，到不了存储
    /// </remarks>
    public static TheoryData<string[], string> MissingHeaders => new()
    {
        { [LastModifiedHeader], "ETag" },
        { [ETagHeader], "Last-Modified" },
    };

    [Theory]
    [MemberData(nameof(IncompleteHeaders))]
    public async Task OnTheWire_AHeadAnswerWithoutAnETagOrLastModified_ReturnsStoreUnavailable(string[] headers, string field)
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", headers);
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var head = await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        AssertUnusable(head.Error, "HeadObject", field);
        Assert.StartsWith("HEAD ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains("HeadObject", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MissingHeaders))]
    public async Task OnTheWire_AGetAnswerWithoutAnETagOrLastModified_ReturnsStoreUnavailable(string[] headers, string field)
    {
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, headers);
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        AssertUnusable(got.Error, "GetObject", field);
        Assert.StartsWith("GET ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(IncompleteHeaders))]
    public async Task OnTheWire_ARangedGetAnswerWithoutAnETagOrLastModified_ReturnsStoreUnavailable(string[] headers, string field)
    {
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("206 Partial Content", "ab", [.. headers, "Content-Range: bytes 0-1/3"]);
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), new BlobRange(0, 2), CancellationToken.None);

        AssertUnusable(got.Error, "GetObject", field);
    }

    [Fact]
    public async Task OnTheWire_CompleteAnswers_FillTheMetadataFromTheService()
    {
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, ETagHeader, LastModifiedHeader);
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());
        var expected = new DateTimeOffset(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);

        var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
        await using (got)
        {
            Assert.Equal((ContentETag, expected, 3L), (got.Metadata.ETag, got.Metadata.LastModified, got.Metadata.Length));
        }
    }

    [Fact]
    public async Task OnTheWire_APutAnswerWithoutAnETag_ReturnsStoreUnavailable()
    {
        // 对象可能已经写进去了：不重发，调用方可以 Head 或重写
        await using var endpoint = RecordingHttpListener.Answering("200 OK");
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var put = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes(Content), BlobPutOptions.Default, CancellationToken.None);

        AssertUnusable(put.Error, "PutObject", "ETag");
        Assert.StartsWith("PUT ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
        Assert.Contains("PutObject", Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_APutAnswer_TakesTheLastModifiedFromTheLocalClock()
    {
        // PutObject 的应答本来就没有 Last-Modified：写入结果的修改时间是本机收到应答时的时钟，之后读出的是服务记下的
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 26, 9, 30, 0, TimeSpan.Zero));
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), clock, new LogCapture());

        var written = (await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes(Content), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        Assert.Equal((ContentETag, clock.GetUtcNow(), 3L), (written.ETag, written.LastModified, written.Length));
    }

    [Fact]
    public async Task ExistenceChecks_DoNotNeedTheETagOrLastModified()
    {
        // 批量删除与不覆盖写入之前的 HEAD 只看对象在不在：应答缺这两项照样判定，HeadAsync 本身则返回错误
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake);
        Assert.True((await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes(Content), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        Assert.True((await store.PutAsync(new ContainerName(Bucket), new BlobKey("b.txt"), Encoding.UTF8.GetBytes("b"), BlobPutOptions.Default, CancellationToken.None)).IsSuccess);
        fake.HeadAnswer = static _ => new GetObjectMetadataResponse { HttpStatusCode = HttpStatusCode.OK };

        AssertUnusable((await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).Error, "HeadObject", "ETag");
        var conflict = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("x"), BlobPutOptions.Default with { Overwrite = false }, CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.BlobExists, conflict.Error?.Code);
        var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default with { Overwrite = false }, CancellationToken.None);
        Assert.Equal(ObjectStorageErrors.BlobExists, started.Error?.Code);

        var deleted = await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey(Key), new BlobKey("missing.txt"), new BlobKey("b.txt")], CancellationToken.None);
        Assert.Equal([true, false, true], deleted.Select(static result => result.ValueOrThrow()));
        Assert.False(fake.Contains(Bucket, Key));
    }

    [Fact]
    public async Task ACompletedUpload_WhoseHeadAnswerIsIncomplete_ReturnsStoreUnavailable()
    {
        // 完成之后再 HEAD 取元数据：缺了 ETag 返回错误，对象已经拼成，调用方可以再 Head
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var part = (await store.UploadPartAsync(upload, 1, new MemoryStream([1, 2, 3]), CancellationToken.None)).ValueOrThrow();
        fake.HeadAnswer = static answer => new GetObjectMetadataResponse { LastModified = answer.LastModified, HttpStatusCode = answer.HttpStatusCode };

        var completed = await store.CompleteAsync(upload, [part], CancellationToken.None);

        AssertUnusable(completed.Error, "HeadObject", "ETag");
        Assert.True(fake.Contains(Bucket, Key));
        Assert.Equal(["InitiateMultipartUpload", "UploadPart", "CompleteMultipartUpload", "GetObjectMetadata"], fake.Requests);
    }

    /// <summary>
    /// 断言是缺内容的应答：存储不可用，<c>reason</c> 点名请求与字段，不带应答里的值
    /// </summary>
    private static void AssertUnusable(Error? error, string operation, string field)
    {
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, error?.Code);
        var reason = error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Contains(operation, reason, StringComparison.Ordinal);
        Assert.Contains(field, reason, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// 指向回环对端、不探测条件写入、只尝试一次的选项
    /// </summary>
    private static S3ObjectStorageOptions Options(int port) => new()
    {
        ServiceUrl = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
        AccessKey = "ak",
        SecretKey = "sk",
        ConditionalWrites = false,
        MaxAttempts = 1,
    };

    /// <summary>
    /// 以替身为客户端的存储
    /// </summary>
    /// <remarks>
    /// 不探测条件写入：条件写入开启即视作服务认它，不覆盖的写入照常走 HEAD 与条件写入
    /// </remarks>
    private static S3BlobStore Store(FakeS3Client fake)
        => new(fake.AsClient(), new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", HealthBucket = Bucket }, TimeProvider.System, new LogCapture());
}

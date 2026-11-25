// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 上传分片与列出分片的应答缺必需的内容时返回错误（ADR-0308）
/// </summary>
/// <remarks>
/// 服务回了成功，上传分片的应答却没有可用的 ETag、列出的分片没有可用的编号或 ETag、长度为负，是不合规的应答：同发起缺上传标识（ADR-0302）一样按服务端错误返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，不把分片构造的 <see cref="ArgumentException"/> 抛给调用方。线上的两条走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>；其余用内存替身 <see cref="FakeS3Client"/>。都不连 S3 服务
/// </remarks>
public sealed class S3MultipartPartResponseTests
{
    /// <summary>
    /// 用例的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 用例的键
    /// </summary>
    private const string Key = "video.mp4";

    /// <summary>
    /// 线上用例里句柄的上传标识
    /// </summary>
    private const string UploadId = "upload-1";

    /// <summary>
    /// 上传分片的请求名
    /// </summary>
    private const string UploadPartRequest = "UploadPart";

    /// <summary>
    /// 列出分片的请求名
    /// </summary>
    private const string ListPartsRequest = "ListParts";

    /// <summary>
    /// 列出分片的应答里当不了分片的那一片，与 <c>reason</c> 里点名的字段
    /// </summary>
    /// <remarks>
    /// 编号缺失或不在 1–10000 之内，ETag 缺失或去掉引号之后为空，长度为负
    /// </remarks>
    public static TheoryData<int?, string?, long?, string> UnusableParts => new()
    {
        { null, "\"a\"", 5, "PartNumber" },
        { 0, "\"a\"", 5, "PartNumber" },
        { -1, "\"a\"", 5, "PartNumber" },
        { BlobMultipart.MaxPartNumber + 1, "\"a\"", 5, "PartNumber" },
        { 1, null, 5, "ETag" },
        { 1, string.Empty, 5, "ETag" },
        { 1, "\"\"", 5, "ETag" },
        { 1, " ", 5, "ETag" },
        { 1, "\"a\"", -1, "Size" },
    };

    [Fact]
    public async Task OnTheWire_AnUploadPartAnswerWithoutAnETag_ReturnsStoreUnavailable()
    {
        // 对端回 200 而没有 ETag 头：SDK 照收，应答里的 ETag 为空（以前分片的构造抛 ArgumentException）
        await using var endpoint = RecordingHttpListener.Answering("200 OK");
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var uploaded = await store.UploadPartAsync(Upload(), 1, new MemoryStream([1, 2, 3]), CancellationToken.None);

        // 只发了上传分片的那一个请求，没有重试
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, uploaded.Error?.Code);
        var request = Assert.Single(endpoint.Requests);
        Assert.StartsWith("PUT ", request, StringComparison.Ordinal);
        Assert.Contains("partNumber=1", request, StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains(UploadPartRequest, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_AListedPartWithoutANumber_ReturnsStoreUnavailable()
    {
        // 对端列出的分片没有 PartNumber：SDK 照收，编号为空（以前按 0 交给编号的核对，抛 ArgumentOutOfRangeException）
        const string body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ListPartsResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Bucket>uploads</Bucket><Key>video.mp4</Key><UploadId>upload-1</UploadId><IsTruncated>false</IsTruncated><Part><LastModified>2026-11-26T00:00:00.000Z</LastModified><ETag>"a"</ETag><Size>5</Size></Part></ListPartsResult>
            """;
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", body, "Content-Type: application/xml");
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var listed = await store.ListPartsAsync(Upload(), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        Assert.Contains("PartNumber", listed.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
        var request = Assert.Single(endpoint.Requests);
        Assert.StartsWith("GET ", request, StringComparison.Ordinal);
        Assert.Contains("uploadId=upload-1", request, StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains(ListPartsRequest, warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"\"")]
    [InlineData("   ")]
    [InlineData("\" \"")]
    public async Task AnUploadPartAnswerWithAnUnusableETag_ReturnsStoreUnavailable_NamingTheRequest(string? etag)
    {
        // 缺失、为空、只有引号、只有空白、引号里只有空白：去掉两端的引号之后为空或只有空白
        var fake = FakeS3Client.Create(Bucket);
        fake.NextPartETag = () => etag;
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var uploaded = await store.UploadPartAsync(upload, 1, new MemoryStream([1, 2, 3]), CancellationToken.None);

        // reason 说明是哪一步的应答缺了什么，不带应答里的值；不重发
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, uploaded.Error?.Code);
        var reason = uploaded.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Contains(UploadPartRequest, reason, StringComparison.Ordinal);
        Assert.Contains("ETag", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", reason, StringComparison.Ordinal);
        Assert.Equal(["InitiateMultipartUpload", UploadPartRequest], fake.Requests);
    }

    [Theory]
    [InlineData("\"abc\"", "abc")]
    [InlineData("abc", "abc")]
    public async Task AnUploadPartAnswerWithAnETag_IsTheETagOfThePart(string answered, string expected)
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.NextPartETag = () => answered;
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var part = (await store.UploadPartAsync(upload, 2, new MemoryStream([1, 2, 3]), CancellationToken.None)).ValueOrThrow();

        Assert.Equal((2, expected, 3L), (part.PartNumber, part.ETag, part.Length));
    }

    [Theory]
    [MemberData(nameof(UnusableParts))]
    public async Task AListedPartThatIsNotUsable_FailsTheWholeListing_NamingTheField(int? number, string? etag, long? size, string field)
    {
        // 坏的那一片排在两片好的中间：整个列出失败，不跳过它（跳过之后续传会重传它，或让完成清单少一片）
        var fake = FakeS3Client.Create(Bucket);
        fake.ListedParts = parts => [.. parts.Take(1), new PartDetail { PartNumber = number, ETag = etag, Size = size }, .. parts.Skip(1)];
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        Assert.True((await store.UploadPartAsync(upload, 1, new MemoryStream([1]), CancellationToken.None)).IsSuccess);
        Assert.True((await store.UploadPartAsync(upload, 3, new MemoryStream([3]), CancellationToken.None)).IsSuccess);

        var listed = await store.ListPartsAsync(upload, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        var reason = listed.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Contains(ListPartsRequest, reason, StringComparison.Ordinal);
        Assert.Contains(field, reason, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", reason, StringComparison.Ordinal);
        Assert.Equal(ListPartsRequest, fake.Requests[^1]);
    }

    [Fact]
    public async Task OnTheWire_ATruncatedListingWithoutANextMarker_ReturnsStoreUnavailable_AfterOneRequest()
    {
        // 对端说截断了却不给 NextPartNumberMarker（ADR-0313）：以前从头重新请求，循环不止、列表一直增长，直到调用方取消
        const string body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ListPartsResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Bucket>uploads</Bucket><Key>video.mp4</Key><UploadId>upload-1</UploadId><IsTruncated>true</IsTruncated><Part><PartNumber>1</PartNumber><LastModified>2026-11-26T00:00:00.000Z</LastModified><ETag>"a"</ETag><Size>5</Size></Part></ListPartsResult>
            """;
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", body, "Content-Type: application/xml");
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var listed = await store.ListPartsAsync(Upload(), deadline.Token);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        Assert.Contains("NextPartNumberMarker", listed.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
        Assert.Single(endpoint.Requests);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains(ListPartsRequest, warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task ATruncatedPageWhoseNextMarkerDoesNotAdvance_FailsTheListing(int? next)
    {
        // 第一页给出标记 1，第二页从 1 开始，又说截断了而标记缺失、不比 1 大：再请求只会回到原处（ADR-0313）
        var fake = FakeS3Client.Create(Bucket);
        var markers = new List<string?>();
        fake.PartPages = (request, _) =>
        {
            markers.Add(request.PartNumberMarker);
            return request.PartNumberMarker is null
                ? Page(true, 1, 1)
                : new ListPartsResponse { Parts = [Part(2)], IsTruncated = true, NextPartNumberMarker = next };
        };
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var listed = await store.ListPartsAsync(upload, deadline.Token);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        Assert.Contains("NextPartNumberMarker", listed.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
        Assert.Equal([null, "1"], markers);
    }

    [Theory]
    [InlineData(BlobMultipart.MaxPartNumber)]
    [InlineData(BlobMultipart.MaxPartNumber + 1)]
    public async Task ATruncatedPageEndingAtTheLastPartNumber_FailsTheListing(int next)
    {
        // 截断说明后面还有编号更大的分片，标记到了 10000 就不可能还有下一页
        var fake = FakeS3Client.Create(Bucket);
        fake.PartPages = (_, _) => new ListPartsResponse { Parts = [Part(BlobMultipart.MaxPartNumber)], IsTruncated = true, NextPartNumberMarker = next };
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var listed = await store.ListPartsAsync(upload, deadline.Token);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        Assert.Contains("NextPartNumberMarker", listed.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
        Assert.Single(fake.Requests, static request => request == ListPartsRequest);
    }

    [Fact]
    public async Task MorePartsThanAnUploadHolds_FailTheListing()
    {
        // 标记每页都前进、每页 1000 片而编号重复：一个上传最多 10000 片，列到第 11 页即失败，不把 12000 片交给调用方
        var fake = FakeS3Client.Create(Bucket);
        fake.PartPages = static (request, _) =>
        {
            var page = request.PartNumberMarker is { } marker ? int.Parse(marker, CultureInfo.InvariantCulture) : 0;
            return new ListPartsResponse { Parts = [.. Enumerable.Range(1, 1000).Select(Part)], IsTruncated = page < 11, NextPartNumberMarker = page + 1 };
        };
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var listed = await store.ListPartsAsync(upload, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        Assert.Contains("10000", listed.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
        Assert.Equal(11, fake.Requests.Count(static request => request == ListPartsRequest));
    }

    [Fact]
    public async Task APagedListing_FollowsEachNextMarker_AndListsEveryPart()
    {
        var fake = FakeS3Client.Create(Bucket);
        var markers = new List<string?>();
        fake.PartPages = (request, _) =>
        {
            markers.Add(request.PartNumberMarker);
            return request.PartNumberMarker switch
            {
                null => Page(true, 2, 1, 2),
                "2" => Page(true, 4, 3, 4),
                _ => Page(false, null, 5),
            };
        };
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var parts = (await store.ListPartsAsync(upload, CancellationToken.None)).ValueOrThrow();

        Assert.Equal([1, 2, 3, 4, 5], parts.Select(static part => part.PartNumber));
        Assert.Equal([null, "2", "4"], markers);
    }

    [Fact]
    public async Task ListedParts_WithoutASize_AreListedWithLengthZero()
    {
        // 长度只用于说明、完成时不核对：缺失照旧当作 0，不算不合规
        var fake = FakeS3Client.Create(Bucket);
        fake.ListedParts = static parts => [.. parts.Select(static part => new PartDetail { PartNumber = part.PartNumber, ETag = part.ETag })];
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        Assert.True((await store.UploadPartAsync(upload, 2, new MemoryStream([1, 2]), CancellationToken.None)).IsSuccess);
        Assert.True((await store.UploadPartAsync(upload, 1, new MemoryStream([1]), CancellationToken.None)).IsSuccess);

        var parts = (await store.ListPartsAsync(upload, CancellationToken.None)).ValueOrThrow();

        Assert.Equal(new[] { (1, "part", 0L), (2, "part", 0L) }, parts.Select(static part => (part.PartNumber, part.ETag, part.Length)));
    }

    [Fact]
    public async Task ListedParts_AtTheEdgesOfTheNumberRange_AreListed()
    {
        // 编号 1 与 10000 都当得了分片：越界的判定不误伤边界
        var fake = FakeS3Client.Create(Bucket);
        fake.ListedParts = static _ => [new PartDetail { PartNumber = 1, ETag = "\"a\"", Size = 1 }, new PartDetail { PartNumber = BlobMultipart.MaxPartNumber, ETag = "\"b\"", Size = 2 }];
        var store = Store(fake);
        var upload = (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();

        var parts = (await store.ListPartsAsync(upload, CancellationToken.None)).ValueOrThrow();

        Assert.Equal(new[] { (1, "a", 1L), (BlobMultipart.MaxPartNumber, "b", 2L) }, parts.Select(static part => (part.PartNumber, part.ETag, part.Length)));
    }

    /// <summary>
    /// 列出分片的一页应答
    /// </summary>
    private static ListPartsResponse Page(bool truncated, int? next, params int[] numbers)
        => new() { Parts = [.. numbers.Select(Part)], IsTruncated = truncated, NextPartNumberMarker = next };

    /// <summary>
    /// 一个可用的分片
    /// </summary>
    private static PartDetail Part(int number) => new() { PartNumber = number, ETag = "\"p\"", Size = 1 };

    /// <summary>
    /// 线上用例的句柄
    /// </summary>
    private static BlobMultipartUpload Upload() => new(new ContainerName(Bucket), new BlobKey(Key), UploadId) { Overwrite = true };

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
    /// 不探测条件写入：条件写入开启即视作服务认它
    /// </remarks>
    private static S3BlobStore Store(FakeS3Client fake)
        => new(fake.AsClient(), new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", HealthBucket = Bucket }, TimeProvider.System, new LogCapture());
}

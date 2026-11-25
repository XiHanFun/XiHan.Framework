// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 列举的应答缺必需的内容时返回错误（ADR-0313）
/// </summary>
/// <remarks>
/// <c>ListObjectsV2</c> 的应答说截断了却没有可用的续页标记，或列出的对象没有可用的 ETag、修改时间，是不合规的应答：按服务端错误返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，不交出一页看上去列完了或元数据残缺的结果。走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>，不连 S3 服务
/// </remarks>
public sealed class S3ListingResponseTests
{
    /// <summary>
    /// 用例的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 列举的请求名
    /// </summary>
    private const string ListRequest = "ListObjectsV2";

    /// <summary>
    /// 一个完整的对象条目
    /// </summary>
    private const string CompleteEntry = """<Contents><Key>a.txt</Key><LastModified>2026-11-26T00:00:00.000Z</LastModified><ETag>"a"</ETag><Size>3</Size></Contents>""";

    [Fact]
    public async Task ATruncatedPageWithoutANextContinuationToken_ReturnsStoreUnavailable()
    {
        // 以前交出续页标记为空的一页，调用方以为列完了
        await using var endpoint = Answering(Body(truncated: true, next: null, CompleteEntry));
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var listed = await store.ListAsync(new ContainerName(Bucket), new BlobQuery(), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        var reason = listed.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Contains(ListRequest, reason, StringComparison.Ordinal);
        Assert.Contains("NextContinuationToken", reason, StringComparison.Ordinal);
        Assert.Single(endpoint.Requests);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains(ListRequest, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATruncatedPageGivingBackTheSameContinuationToken_ReturnsStoreUnavailable()
    {
        // 续页标记与本次请求带的相同：调用方拿它再列只会回到原处，循环不止
        await using var endpoint = Answering(Body(truncated: true, next: "token-1", CompleteEntry));
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var listed = await store.ListAsync(new ContainerName(Bucket), new BlobQuery { ContinuationToken = "token-1" }, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        Assert.Contains("NextContinuationToken", listed.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("token-1", listed.Error.Details?["reason"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATruncatedPageWithANewContinuationToken_HandsItOver()
    {
        await using var endpoint = Answering(Body(truncated: true, next: "token-2", CompleteEntry));
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var page = (await store.ListAsync(new ContainerName(Bucket), new BlobQuery { ContinuationToken = "token-1" }, CancellationToken.None)).ValueOrThrow();

        Assert.Equal("token-2", page.ContinuationToken);
        var item = Assert.Single(page.Items);
        Assert.Equal(("a.txt", "a", 3L, new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero)), (item.Key.Value, item.ETag, item.Length, item.LastModified));
    }

    [Theory]
    [InlineData("""<Contents><Key>b.txt</Key><LastModified>2026-11-26T00:00:00.000Z</LastModified><Size>3</Size></Contents>""", "ETag")]
    [InlineData("""<Contents><Key>b.txt</Key><LastModified>2026-11-26T00:00:00.000Z</LastModified><ETag>""</ETag><Size>3</Size></Contents>""", "ETag")]
    [InlineData("""<Contents><Key>b.txt</Key><ETag>"b"</ETag><Size>3</Size></Contents>""", "LastModified")]
    public async Task AListedObjectWithoutAUsableETagOrLastModified_FailsTheWholePage(string entry, string field)
    {
        // 以前 ETag 是空串、修改时间是 DateTimeOffset.MinValue；不跳过它：跳过之后的一页少了一个对象，调用方看不出来
        await using var endpoint = Answering(Body(truncated: false, next: null, CompleteEntry, entry));
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var listed = await store.ListAsync(new ContainerName(Bucket), new BlobQuery(), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, listed.Error?.Code);
        var reason = listed.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Contains(ListRequest, reason, StringComparison.Ordinal);
        Assert.Contains(field, reason, StringComparison.Ordinal);
        Assert.DoesNotContain("b.txt", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnObjectOutsideTheContract_IsSkipped_EvenWithoutAnETagOrLastModified()
    {
        // 目录占位与保留前缀下的对象不是本契约的对象：照旧跳过，它们缺什么不影响这一页
        const string placeholder = """<Contents><Key>dir/</Key><Size>0</Size></Contents>""";
        await using var endpoint = Answering(Body(truncated: false, next: null, placeholder, CompleteEntry));
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var page = (await store.ListAsync(new ContainerName(Bucket), new BlobQuery(), CancellationToken.None)).ValueOrThrow();

        Assert.Equal("a.txt", Assert.Single(page.Items).Key.Value);
        Assert.Null(page.ContinuationToken);
    }

    /// <summary>
    /// 列举应答的 XML
    /// </summary>
    private static string Body(bool truncated, string? next, params string[] entries)
        => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><Name>uploads</Name><Prefix></Prefix>"
            + string.Create(CultureInfo.InvariantCulture, $"<KeyCount>{entries.Length}</KeyCount><MaxKeys>1000</MaxKeys><IsTruncated>{(truncated ? "true" : "false")}</IsTruncated>")
            + string.Concat(entries)
            + (next is null ? string.Empty : "<NextContinuationToken>" + next + "</NextContinuationToken>")
            + "</ListBucketResult>";

    /// <summary>
    /// 每个请求都回这份列举应答的回环对端
    /// </summary>
    private static RecordingHttpListener Answering(string body) => RecordingHttpListener.AnsweringWithBody("200 OK", body, "Content-Type: application/xml");

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
}

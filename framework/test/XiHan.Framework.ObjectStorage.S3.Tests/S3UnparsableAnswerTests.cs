// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// SDK 解析不了的应答按不合规的应答返回错误（ADR-0322、ADR-0333）
/// </summary>
/// <remarks>
/// AWS SDK for .NET 4.0 的异步解组不包装异常：应答头或响应体里按类型读的值写错了（<c>Last-Modified</c> 不是日期，<c>Content-Length</c>、<c>x-amz-mp-parts-count</c> 不是数或越界，布尔值的头不是 <c>true</c> / <c>false</c>，XML 里的数不是数），它原样抛出 <see cref="FormatException"/> 或 <see cref="OverflowException"/>。响应体不是良构的 XML 时它原样抛出 <see cref="XmlException"/>（ADR-0333）。以前这三类异常原样抛给调用方；现在每一步都返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，<c>reason</c> 只写请求名，不带写错的值。走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>，不连 S3 服务
/// </remarks>
public sealed class S3UnparsableAnswerTests
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
    /// 应答里的 ETag 头（内容 <c>abc</c> 的 MD5）
    /// </summary>
    private const string ETagHeader = "ETag: \"900150983cd24fb0d6963f7d28e17f72\"";

    /// <summary>
    /// 应答里的修改时间头
    /// </summary>
    private const string LastModifiedHeader = "Last-Modified: Thu, 26 Nov 2026 08:00:00 GMT";

    /// <summary>
    /// 写错的布尔值头：写入、上传分片与完成的应答都按布尔值读它
    /// </summary>
    private const string BadBooleanHeader = "x-amz-server-side-encryption-bucket-key-enabled: maybe";

    /// <summary>
    /// 读取与 HEAD 的应答里写错的头，与它是不是原样写出（不另加 <c>Content-Length</c>）
    /// </summary>
    /// <remarks>
    /// 修改时间不是日期、分片数不是数或超出 32 位、布尔值不是 <c>true</c> / <c>false</c>、长度不是数或超出 64 位
    /// </remarks>
    public static TheoryData<string, bool> BadObjectHeaders => new()
    {
        { "Last-Modified: not a date", false },
        { "x-amz-mp-parts-count: many", false },
        { "x-amz-mp-parts-count: 99999999999", false },
        { BadBooleanHeader, false },
        { "Content-Length: many", true },
        { "Content-Length: 99999999999999999999", true },
    };

    [Theory]
    [MemberData(nameof(BadObjectHeaders))]
    public async Task OnTheWire_AHeadAnswerTheSdkCannotParse_ReturnsStoreUnavailable(string header, bool verbatim)
    {
        await using var endpoint = verbatim
            ? RecordingHttpListener.AnsweringVerbatim("200 OK", string.Empty, ObjectHeaders(header))
            : RecordingHttpListener.Answering("200 OK", ObjectHeaders(header));
        var logs = new LogCapture();
        using var store = Store(endpoint, logs);

        var head = await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        AssertUnparsable(head.Error, "HeadObject", header);
        Assert.StartsWith("HEAD ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains("HeadObject", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BadObjectHeaders))]
    public async Task OnTheWire_ARangedReadAnswerTheSdkCannotParse_ReturnsStoreUnavailable(string header, bool verbatim)
    {
        await using var endpoint = verbatim
            ? RecordingHttpListener.AnsweringVerbatim("206 Partial Content", "ab", [.. ObjectHeaders(header), "Content-Range: bytes 0-1/3"])
            : RecordingHttpListener.AnsweringWithBody("206 Partial Content", "ab", [.. ObjectHeaders(header), "Content-Range: bytes 0-1/3"]);
        using var store = Store(endpoint, new LogCapture());

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), new BlobRange(0, 2), CancellationToken.None);

        AssertUnparsable(got.Error, "GetObject", header);
        Assert.Single(endpoint.Requests);
    }

    [Theory]
    [MemberData(nameof(BadObjectHeaders))]
    public async Task OnTheWire_AWholeReadAnswerTheSdkCannotParse_ReturnsStoreUnavailable(string header, bool verbatim)
    {
        // 整读时 FormatException 也可能来自把 ETag 当 MD5 解码：不靠调用栈分不出来，reason 同时点名两者；OverflowException 只来自读头
        await using var endpoint = verbatim
            ? RecordingHttpListener.AnsweringVerbatim("200 OK", "abc", ObjectHeaders(header))
            : RecordingHttpListener.AnsweringWithBody("200 OK", "abc", ObjectHeaders(header));
        using var store = Store(endpoint, new LogCapture());

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        AssertUnparsable(got.Error, "GetObject", header);
        var reason = got.Error!.Details!["reason"]?.ToString() ?? string.Empty;
        Assert.Equal(!header.Contains("9999", StringComparison.Ordinal), reason.Contains("hexadecimal", StringComparison.Ordinal));
        Assert.Single(endpoint.Requests);
    }

    [Fact]
    public async Task OnTheWire_AWriteAnswerTheSdkCannotParse_ReturnsStoreUnavailable_WithoutRetrying()
    {
        // 对象可能已经写进去了：不重发、不回滚，同写入缺 ETag
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader, BadBooleanHeader);
        using var store = Store(endpoint, new LogCapture());

        var put = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), BlobPutOptions.Default, CancellationToken.None);

        AssertUnparsable(put.Error, "PutObject", BadBooleanHeader);
        Assert.StartsWith("PUT ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_AListingTheSdkCannotParse_FailsTheWholePage()
    {
        const string body = """<?xml version="1.0" encoding="UTF-8"?><ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Name>uploads</Name><KeyCount>1</KeyCount><MaxKeys>1000</MaxKeys><IsTruncated>false</IsTruncated><Contents><Key>a.txt</Key><LastModified>2026-11-26T00:00:00.000Z</LastModified><ETag>"a"</ETag><Size>three</Size></Contents></ListBucketResult>""";
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", body, "Content-Type: application/xml");
        using var store = Store(endpoint, new LogCapture());

        var listed = await store.ListAsync(new ContainerName(Bucket), new BlobQuery(), CancellationToken.None);

        AssertUnparsable(listed.Error, "ListObjectsV2", "three");
    }

    [Fact]
    public async Task OnTheWire_MultipartAnswersTheSdkCannotParse_ReturnStoreUnavailable_NamingTheStep()
    {
        var upload = new BlobMultipartUpload(new ContainerName(Bucket), new BlobKey(Key), "upload-1") { Overwrite = true };

        // 发起：放弃日期不是日期
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", """<?xml version="1.0" encoding="UTF-8"?><InitiateMultipartUploadResult><Bucket>uploads</Bucket><Key>a.txt</Key><UploadId>upload-1</UploadId></InitiateMultipartUploadResult>""", "Content-Type: application/xml", "x-amz-abort-date: soon"))
        {
            using var store = Store(endpoint, new LogCapture());
            var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None);
            AssertUnparsable(started.Error, "InitiateMultipartUpload", "soon");
            Assert.Single(endpoint.Requests);
        }

        // 上传分片：布尔值的头写错
        await using (var endpoint = RecordingHttpListener.Answering("200 OK", "ETag: \"p\"", BadBooleanHeader))
        {
            using var store = Store(endpoint, new LogCapture());
            var part = await store.UploadPartAsync(upload, 1, new MemoryStream([1, 2, 3]), CancellationToken.None);
            AssertUnparsable(part.Error, "UploadPart", BadBooleanHeader);
        }

        // 列出分片：编号不是数
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", """<?xml version="1.0" encoding="UTF-8"?><ListPartsResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Bucket>uploads</Bucket><Key>a.txt</Key><UploadId>upload-1</UploadId><IsTruncated>false</IsTruncated><Part><PartNumber>first</PartNumber><ETag>"p"</ETag><Size>3</Size></Part></ListPartsResult>""", "Content-Type: application/xml"))
        {
            using var store = Store(endpoint, new LogCapture());
            var listed = await store.ListPartsAsync(upload, CancellationToken.None);
            AssertUnparsable(listed.Error, "ListParts", "first");
        }

        // 完成：对象可能已经拼成，不重发
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", """<?xml version="1.0" encoding="UTF-8"?><CompleteMultipartUploadResult><Bucket>uploads</Bucket><Key>a.txt</Key><ETag>"c-1"</ETag></CompleteMultipartUploadResult>""", "Content-Type: application/xml", BadBooleanHeader))
        {
            using var store = Store(endpoint, new LogCapture());
            var completed = await store.CompleteAsync(upload, [new BlobPart(1, "p", 3)], CancellationToken.None);
            AssertUnparsable(completed.Error, "CompleteMultipartUpload", BadBooleanHeader);
            Assert.Single(endpoint.Requests);
        }
    }

    [Fact]
    public async Task OnTheWire_ADeleteAnswerTheSdkCannotParse_FailsEveryKeyOfThatBatch()
    {
        // 先 HEAD 定存在，再一次 DeleteObjects：删除的应答解析不了，这一批里哪些删成了说不清，同这一批的请求整个失败
        const string deleted = """<?xml version="1.0" encoding="UTF-8"?><DeleteResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Deleted><Key>a.txt</Key><DeleteMarker>perhaps</DeleteMarker></Deleted></DeleteResult>""";
        await using var endpoint = RecordingHttpListener.AnsweringInTurn(
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader]),
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader]),
            new RecordingHttpListener.Reply("200 OK", deleted, ["Content-Type: application/xml"]));
        using var store = Store(endpoint, new LogCapture());

        var results = await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey(Key), new BlobKey("b.txt")], CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, static result => AssertUnparsable(result.Error, "DeleteObjects", "perhaps"));
        Assert.Equal(3, endpoint.Requests.Count);
    }

    [Fact]
    public async Task OnTheWire_AnExistenceCheckTheSdkCannotParse_FailsThatKeyOnly()
    {
        // 只判存在与否的 HEAD 解析不了：SDK 没有交出应答，存在与否无从判定，这一项失败、不发删除
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader, "Last-Modified: not a date");
        using var store = Store(endpoint, new LogCapture());

        var result = Assert.Single(await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey(Key)], CancellationToken.None));

        AssertUnparsable(result.Error, "HeadObject", "not a date");
        Assert.StartsWith("HEAD ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
    }

    /// <summary>
    /// 不是良构 XML 的响应体，与 reason 里不该出现的片段
    /// </summary>
    /// <remarks>
    /// 截断在元素中间、标签不配对、根元素之后还有内容
    /// </remarks>
    public static TheoryData<string, string> MalformedListings => new()
    {
        { """<?xml version="1.0" encoding="UTF-8"?><ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Name>uploads</Name><KeyCount>1""", "KeyCount" },
        { """<?xml version="1.0" encoding="UTF-8"?><ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Name>uploads</Nmae></ListBucketResult>""", "Nmae" },
        { """<?xml version="1.0" encoding="UTF-8"?><ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Name>uploads</Name><IsTruncated>false</IsTruncated></ListBucketResult><extra/>""", "extra" },
    };

    [Theory]
    [MemberData(nameof(MalformedListings))]
    public async Task OnTheWire_AListingThatIsNotWellFormedXml_FailsTheWholePage(string body, string fragment)
    {
        // 以前 SDK 的 XmlException 原样抛给调用方（ADR-0333）
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", body, "Content-Type: application/xml");
        var logs = new LogCapture();
        using var store = Store(endpoint, logs);

        var listed = await store.ListAsync(new ContainerName(Bucket), new BlobQuery(), CancellationToken.None);

        AssertUnparsable(listed.Error, "ListObjectsV2", fragment);
        Assert.Contains("XML", listed.Error!.Details!["reason"]?.ToString(), StringComparison.Ordinal);
        Assert.Contains("ListObjectsV2", Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_MultipartAnswersThatAreNotWellFormedXml_ReturnStoreUnavailable_NamingTheStep()
    {
        var upload = new BlobMultipartUpload(new ContainerName(Bucket), new BlobKey(Key), "upload-1") { Overwrite = true };

        // 发起：不重发，服务端可能已经建了上传
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", """<?xml version="1.0" encoding="UTF-8"?><InitiateMultipartUploadResult><Bucket>uploads</Bucket><UploadId>upload-1""", "Content-Type: application/xml"))
        {
            using var store = Store(endpoint, new LogCapture());
            var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), BlobPutOptions.Default, CancellationToken.None);
            AssertUnparsable(started.Error, "InitiateMultipartUpload", "upload-1");
            Assert.Single(endpoint.Requests);
        }

        // 列出分片
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", """<?xml version="1.0" encoding="UTF-8"?><ListPartsResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Part><PartNumber>1</PartNumber></Prat></ListPartsResult>""", "Content-Type: application/xml"))
        {
            using var store = Store(endpoint, new LogCapture());
            var listed = await store.ListPartsAsync(upload, CancellationToken.None);
            AssertUnparsable(listed.Error, "ListParts", "Prat");
        }

        // 完成：对象可能已经拼成，不重发
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", """<?xml version="1.0" encoding="UTF-8"?><CompleteMultipartUploadResult><Bucket>uploads</Bucket><ETag>"c-1"</ETag>""", "Content-Type: application/xml"))
        {
            using var store = Store(endpoint, new LogCapture());
            var completed = await store.CompleteAsync(upload, [new BlobPart(1, "p", 3)], CancellationToken.None);
            AssertUnparsable(completed.Error, "CompleteMultipartUpload", "c-1");
            Assert.Single(endpoint.Requests);
        }
    }

    [Fact]
    public async Task OnTheWire_ADeleteAnswerThatIsNotWellFormedXml_FailsEveryKeyOfThatBatch()
    {
        // 这一批里哪些删成了说不清，同这一批的请求整个失败
        await using var endpoint = RecordingHttpListener.AnsweringInTurn(
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader]),
            new RecordingHttpListener.Reply("200 OK", """<?xml version="1.0" encoding="UTF-8"?><DeleteResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Error><Key>a.txt""", ["Content-Type: application/xml"]));
        using var store = Store(endpoint, new LogCapture());

        var result = Assert.Single(await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey(Key)], CancellationToken.None));

        AssertUnparsable(result.Error, "DeleteObjects", "<Error>");
        Assert.Equal(2, endpoint.Requests.Count);
    }

    [Fact]
    public void OnlyTheSdkParseFailures_AreTakenForAnUnusableAnswer()
    {
        // 只接解组时的这三类（写坏的 XML 自 ADR-0333 起也算）：端点写错的 UriFormatException 与编程错误都不算，照旧抛出
        Assert.True(S3BlobStore.IsUnparsableAnswer(new FormatException()));
        Assert.True(S3BlobStore.IsUnparsableAnswer(new OverflowException()));
        Assert.True(S3BlobStore.IsUnparsableAnswer(new XmlException()));
        Assert.False(S3BlobStore.IsUnparsableAnswer(new UriFormatException()));
        Assert.False(S3BlobStore.IsUnparsableAnswer(new ArgumentException("x", "value")));
        Assert.False(S3BlobStore.IsUnparsableAnswer(new InvalidOperationException()));
    }

    /// <summary>
    /// 断言是解析不了的应答：存储不可用，<c>reason</c> 点名请求、说明解析不了，不带写错的值
    /// </summary>
    private static void AssertUnparsable(Error? error, string operation, string badValue)
    {
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, error?.Code);
        var reason = error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.StartsWith(operation + " ", reason, StringComparison.Ordinal);
        Assert.Contains("could not parse", reason, StringComparison.Ordinal);
        var value = badValue[(badValue.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
        Assert.DoesNotContain(value, reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// 读取与 HEAD 的应答头：完整的 ETag 与修改时间，加上写错的那一个
    /// </summary>
    /// <remarks>
    /// 写错的是修改时间时不再带对的那一个：同名的头有两个时 SDK 只读第一个
    /// </remarks>
    private static string[] ObjectHeaders(string header)
        => header.StartsWith("Last-Modified:", StringComparison.Ordinal) ? [ETagHeader, header] : [ETagHeader, LastModifiedHeader, header];

    /// <summary>
    /// 指向回环对端、不探测条件写入、只尝试一次的存储
    /// </summary>
    /// <remarks>
    /// 客户端归存储所有，释放存储即释放它
    /// </remarks>
    private static S3BlobStore Store(RecordingHttpListener endpoint, LogCapture logs)
    {
        var options = new S3ObjectStorageOptions
        {
            ServiceUrl = $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}",
            AccessKey = "ak",
            SecretKey = "sk",
            ConditionalWrites = false,
            MaxAttempts = 1,
        };
        var client = S3ObjectStorageFeature.CreateClient(options);
        return new S3BlobStore(client, options, TimeProvider.System, logs, S3ConditionalWriteGate.Unprobed(client, options, TimeProvider.System), owned: true);
    }
}

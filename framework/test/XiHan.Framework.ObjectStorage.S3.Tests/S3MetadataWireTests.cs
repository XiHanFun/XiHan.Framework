// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 自定义元数据的值在线上的编解码，存不成原样与收不下时的校验错误
/// </summary>
/// <remarks>
/// 照 RFC 2047 编码与解码（ADR-0333）。整个是 encoded-word 形状的只有 ASCII 的值写入与分片上传的发起返回 <see cref="ObjectStorageErrors.MetadataValueUnsupported"/>、一个请求都不发；服务以 400 <c>MetadataTooLarge</c> 拒收时返回 <see cref="ObjectStorageErrors.MetadataTooLarge"/>，以前按存储故障返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>（ADR-0343）。以前值原样写进 <c>x-amz-meta-*</c> 请求头：有非 ASCII 字符时 .NET 的 HTTP 客户端不发出这个头，写入以 <see cref="ObjectStorageErrors.StoreUnavailable"/> 失败；首尾的空格被 HTTP 去掉；读回的 encoded-word 原样交出。走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>，核对线上的请求头与按应答头解出的元数据，不连 S3 服务。期望的编码由 Python 的 <c>base64</c> 与 UTF-8 编码独立算出
/// </remarks>
public sealed class S3MetadataWireTests
{
    /// <summary>
    /// 用例的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 用例的键
    /// </summary>
    private const string Key = "report.pdf";

    /// <summary>
    /// 应答里的 ETag 头（内容 <c>abc</c> 的 MD5）
    /// </summary>
    private const string ETagHeader = "ETag: \"900150983cd24fb0d6963f7d28e17f72\"";

    /// <summary>
    /// 应答里的修改时间头
    /// </summary>
    private const string LastModifiedHeader = "Last-Modified: Thu, 26 Nov 2026 08:00:00 GMT";

    /// <summary>
    /// 写入的值与线上的请求头值
    /// </summary>
    /// <remarks>
    /// 非 ASCII（含平面以外的字符）、首尾的空格、以 <c>=?</c> 开头且以 <c>?=</c> 结尾而不整个是 encoded-word 形状的 ASCII 编码（首尾有空格的 encoded-word 形状也是）；中间的连续空格、空串、只是含有 <c>=?</c> 的 ASCII 原样写。整个是 encoded-word 形状的不写，见 <see cref="EncodedWordShapedValues"/>（ADR-0343）
    /// </remarks>
    public static TheoryData<string, string> WrittenValues => new()
    {
        { "2026 年度报告", "=?UTF-8?B?MjAyNiDlubTluqbmiqXlkYo=?=" },
        { "\U0001F600", "=?UTF-8?B?8J+YgA==?=" },
        { " lead", "=?UTF-8?B?IGxlYWQ=?=" },
        { "trail ", "=?UTF-8?B?dHJhaWwg?=" },
        { " ", "=?UTF-8?B?IA==?=" },
        { " =?UTF-8?B?5Lit?=", "=?UTF-8?B?ID0/VVRGLTg/Qj81TGl0Pz0=?=" },
        { "=?UTF-8?B?5Lit?= ", "=?UTF-8?B?PT9VVEYtOD9CPzVMaXQ/PSA=?=" },
        { "=?x?=", "=?UTF-8?B?PT94Pz0=?=" },
        { "=?UTF-8?B?5Lit?==?UTF-8?B?5paH?=", "=?UTF-8?B?PT9VVEYtOD9CPzVMaXQ/PT0/VVRGLTg/Qj81cGFIPz0=?=" },
        { "=?UTF-8?B?5Lit?= note", "=?UTF-8?B?5Lit?= note" },
        { "plain", "plain" },
        { "a  b", "a  b" },
        { "", "" },
        { "=? not a word", "=? not a word" },
        { "ends ?=", "ends ?=" },
    };

    /// <summary>
    /// 整个是 encoded-word 形状的只有 ASCII 的值
    /// </summary>
    /// <remarks>
    /// 判定同读回时解码的形状：一个或多个以空格隔开的 encoded-word，认不认得 charset 与 encoding 都算（ADR-0343）
    /// </remarks>
    public static TheoryData<string> EncodedWordShapedValues =>
    [
        "=?UTF-8?B?YQ==?=",
        "=?utf-8?q?a?=",
        "=?UTF-8?B?5Lit?= =?UTF-8?Q?x?=",
        "=?UTF-8?B?5Lit?=  =?UTF-8?B?5paH?=",
        "=?ISO-8859-1?Q?caf=E9?=",
        "=?x?y?z?=",
    ];

    /// <summary>
    /// 服务以元数据太大拒收时的 S3 错误响应体
    /// </summary>
    private const string MetadataTooLargeBody = """<?xml version="1.0" encoding="UTF-8"?><Error><Code>MetadataTooLarge</Code><Message>Your metadata headers exceed the maximum allowed metadata size.</Message><RequestId>R1</RequestId><HostId>H1</HostId></Error>""";

    /// <summary>
    /// 应答头里的值与解出的元数据
    /// </summary>
    /// <remarks>
    /// B 与 Q、大小写、相邻 encoded-word 之间的空白、一个字符拆在两个 encoded-word 里、RFC 2231 的语言后缀、超过 75 个字符的 encoded-word；不整个是 encoded-word 的原样交出
    /// </remarks>
    public static TheoryData<string, string> AnsweredValues => new()
    {
        { "=?UTF-8?B?MjAyNiDlubTluqbmiqXlkYo=?=", "2026 年度报告" },
        { "=?utf-8?b?5Lit5paH?=", "中文" },
        { "=?UTF-8?Q?2026_=E5=B9=B4=E5=BA=A6=E6=8A=A5=E5=91=8A?=", "2026 年度报告" },
        { "=?UTF-8?q?=e4=b8=ad?=", "中" },
        { "=?UTF-8?B?5Lit?= =?UTF-8?B?5paH?=", "中文" },
        { "=?UTF-8?B?5Lit?=\t  =?UTF-8?Q?=E6=96=87_x?=", "中文 x" },
        { "=?UTF-8?B?5Lg=?= =?UTF-8?B?rQ==?=", "中" },
        { "=?UTF-8*zh?B?5Lit?=", "中" },
        { "=?UTF-8?B?" + string.Concat(Enumerable.Repeat("5Lit", 20)) + "?=", string.Concat(Enumerable.Repeat("中", 20)) },
        { "plain", "plain" },
        { "a  b", "a  b" },
        { "note =?UTF-8?B?5Lit?=", "note =?UTF-8?B?5Lit?=" },
        { "=?UTF-8?B?5Lit?= note", "=?UTF-8?B?5Lit?= note" },
        { "=?UTF-8?B?5Lit?==?UTF-8?B?5paH?=", "=?UTF-8?B?5Lit?==?UTF-8?B?5paH?=" },
        { "=?foo?=", "=?foo?=" },
        { "=?UTF-8?B??=", "=?UTF-8?B??=" },
        { "=?UTF.8?B?5Lit?=", "=?UTF.8?B?5Lit?=" },
    };

    /// <summary>
    /// 解不了的 encoded-word
    /// </summary>
    /// <remarks>
    /// 别的字符集、未知的编码、坏的 base64、坏的 Q、不是合法 UTF-8 的字节（含编成 UTF-8 的代理项）、解出控制字符；与能解的放在一起也一样
    /// </remarks>
    public static TheoryData<string> UndecodableValues =>
    [
        "=?ISO-8859-1?Q?caf=E9?=",
        "=?UTF-16?B?AE4t?=",
        "=?UTF-8?X?abc?=",
        "=?UTF-8?B?5Li?=",
        "=?UTF-8?B?5L!t?=",
        "=?UTF-8?Q?=E4=B8?=",
        "=?UTF-8?Q?=G1?=",
        "=?UTF-8?Q?a=?=",
        "=?UTF-8?B?/w==?=",
        "=?UTF-8?B?7aCA?=",
        "=?UTF-8?B?Cg==?=",
        "=?UTF-8?B?5Lit?= =?ISO-8859-1?Q?caf=E9?=",
    ];

    [Theory]
    [MemberData(nameof(WrittenValues))]
    public async Task OnTheWire_AWrite_SendsTheValueEncodedWhereTheHeaderCannotCarryIt(string value, string header)
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader);
        using var store = Store(endpoint, new LogCapture());
        var options = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["title"] = value, ["owner"] = "alice" } };

        var put = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), options, CancellationToken.None);

        // 写入结果是原值；线上是编码之后的值，别的项原样
        Assert.Equal(value, put.ValueOrThrow().Metadata["title"]);
        var request = Assert.Single(endpoint.Requests);
        Assert.Equal(header, RecordingHttpListener.Header(request, "x-amz-meta-title"));
        Assert.Equal("alice", RecordingHttpListener.Header(request, "x-amz-meta-owner"));
    }

    [Theory]
    [InlineData(15, 1)]
    [InlineData(16, 2)]
    [InlineData(680, 46)]
    public async Task OnTheWire_ALongValue_IsSplitIntoEncodedWordsOfAtMost75Characters(int characters, int words)
    {
        // 每个「中」3 个字节，一个 encoded-word 至多 45 个字节：15 个正好一个，16 个拆成两个
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader);
        using var store = Store(endpoint, new LogCapture());
        var value = string.Concat(Enumerable.Repeat("中", characters));
        var options = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["long"] = value } };

        var put = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), options, CancellationToken.None);

        Assert.True(put.IsSuccess);
        var encoded = RecordingHttpListener.Header(Assert.Single(endpoint.Requests), "x-amz-meta-long")!;
        var parts = encoded.Split(' ');
        Assert.Equal(words, parts.Length);
        Assert.All(parts, static part =>
        {
            Assert.InRange(part.Length, 1, 75);
            Assert.StartsWith("=?UTF-8?B?", part, StringComparison.Ordinal);
            Assert.EndsWith("?=", part, StringComparison.Ordinal);
            // 每一个都解得成完整的字符：不把多字节字符拆在两个里
            Assert.Matches("^(中)+$", Encoding.UTF8.GetString(Convert.FromBase64String(part[10..^2])));
        });
        Assert.Equal("=?UTF-8?B?5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit5Lit?=", parts[0]);
    }

    [Fact]
    public async Task OnTheWire_AValueSplitNextToAFourByteCharacter_KeepsTheCharacterWhole()
    {
        // 43 个 a 之后是 U+1F600（4 个字节）：放进第一个就超过 45 个字节，整个挪到第二个
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader);
        using var store = Store(endpoint, new LogCapture());
        var options = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["mixed"] = new string('a', 43) + "\U0001F600" } };

        Assert.True((await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), options, CancellationToken.None)).IsSuccess);

        Assert.Equal(
            "=?UTF-8?B?YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYQ==?= =?UTF-8?B?8J+YgA==?=",
            RecordingHttpListener.Header(Assert.Single(endpoint.Requests), "x-amz-meta-mixed"));
    }

    [Fact]
    public async Task OnTheWire_StartingAnUpload_SendsTheValueEncoded()
    {
        const string body = """<?xml version="1.0" encoding="UTF-8"?><InitiateMultipartUploadResult><Bucket>uploads</Bucket><Key>report.pdf</Key><UploadId>upload-1</UploadId></InitiateMultipartUploadResult>""";
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", body, "Content-Type: application/xml");
        using var store = Store(endpoint, new LogCapture());
        var options = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["title"] = "2026 年度报告" } };

        var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), options, CancellationToken.None);

        Assert.Equal("upload-1", started.ValueOrThrow().UploadId);
        Assert.Equal("=?UTF-8?B?MjAyNiDlubTluqbmiqXlkYo=?=", RecordingHttpListener.Header(Assert.Single(endpoint.Requests), "x-amz-meta-title"));
    }

    [Theory]
    [MemberData(nameof(EncodedWordShapedValues))]
    public async Task OnTheWire_AnEncodedWordShapedValue_IsRefusedBeforeAnyRequest(string value)
    {
        // 原样写出读回时被解码，编码写出 Amazon S3 又先解码再存：宁可报错，不悄悄改值。不覆盖的写入连 HEAD 也不发，分片上传的发起同此
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader);
        using var store = Store(endpoint, new LogCapture());
        var metadata = new Dictionary<string, string> { ["title"] = value, ["owner"] = "alice" };

        var put = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), new BlobPutOptions { Metadata = metadata }, CancellationToken.None);
        var guarded = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), new BlobPutOptions { Metadata = metadata, Overwrite = false }, CancellationToken.None);
        var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), new BlobPutOptions { Metadata = metadata }, CancellationToken.None);

        foreach (var error in new[] { put.Error, guarded.Error, started.Error })
        {
            Assert.Equal(ObjectStorageErrors.MetadataValueUnsupported, error?.Code);
            Assert.Equal(ObjectStorageMessages.MetadataValueUnsupported, error!.MessageKey);
            Assert.Equal("title", Assert.Single(error.Details!).Value);
        }

        Assert.Equal(0, endpoint.ConnectionCount);
    }

    [Fact]
    public async Task OnTheWire_SeveralRefusedValues_NameTheFirstInOrdinalOrder()
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader);
        using var store = Store(endpoint, new LogCapture());
        var options = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["zeta"] = "=?UTF-8?B?YQ==?=", ["plain"] = "ok", ["alpha"] = "=?UTF-8?B?Yg==?=", ["b"] = "=?x?y?z?=" } };

        var put = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), options, CancellationToken.None);

        Assert.Equal("alpha", put.Error?.Details?["name"]);
        Assert.Equal(0, endpoint.ConnectionCount);
    }

    [Fact]
    public async Task OnTheWire_AContentTypeTheStoreCannotWrite_IsReportedBeforeTheMetadata()
    {
        // 两样都写不出原值时先报内容类型：核对的次序固定（RFC-0012「S3 Provider」）
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader);
        using var store = Store(endpoint, new LogCapture());
        var options = new BlobPutOptions { ContentType = "text/plain;", Metadata = new Dictionary<string, string> { ["title"] = "=?UTF-8?B?YQ==?=" } };

        var put = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), options, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.ContentTypeUnsupported, put.Error?.Code);
        Assert.Equal(0, endpoint.ConnectionCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnTheWire_AServiceRefusingTheMetadataAsTooLarge_ReturnsMetadataTooLarge(bool multipart)
    {
        // 680 个「中」合计 2044 个字节，没超契约的上限；编码之后约 3.3 KB，Amazon S3 按头计会以 400 MetadataTooLarge 拒收。
        // 以前按存储故障返回 StoreUnavailable、reason 是异常类型；尝试次数取 3，核对 SDK 不因它重发
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("400 Bad Request", MetadataTooLargeBody, "Content-Type: application/xml");
        var logs = new LogCapture();
        using var store = Store(endpoint, logs, maxAttempts: 3);
        var options = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["long"] = string.Concat(Enumerable.Repeat("中", 680)) } };

        var error = multipart
            ? (await store.StartAsync(new ContainerName(Bucket), new BlobKey(Key), options, CancellationToken.None)).Error
            : (await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes("abc"), options, CancellationToken.None)).Error;

        Assert.Equal(ObjectStorageErrors.MetadataTooLarge, error?.Code);
        Assert.Equal(ObjectStorageMessages.MetadataTooLarge, error!.MessageKey);
        Assert.True(error.Details is null || error.Details.Count == 0);
        Assert.Single(endpoint.Requests);
        // 是调用方能改的输入，不当作存储故障记 Warning
        Assert.DoesNotContain(logs.Entries, static entry => entry.Level >= LogLevel.Warning);
    }

    [Theory]
    [MemberData(nameof(AnsweredValues))]
    public async Task OnTheWire_AHead_DecodesEncodedWords(string answered, string expected)
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader, LastModifiedHeader, "x-amz-meta-title: " + answered, "x-amz-meta-owner: alice");
        using var store = Store(endpoint, new LogCapture());

        var head = (await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();

        Assert.Equal(expected, head.Metadata["title"]);
        Assert.Equal("alice", head.Metadata["owner"]);
    }

    [Fact]
    public async Task OnTheWire_WholeAndRangedReads_DecodeEncodedWords()
    {
        const string header = "x-amz-meta-title: =?UTF-8?Q?2026_=E5=B9=B4=E5=BA=A6=E6=8A=A5=E5=91=8A?=";
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", "abc", ETagHeader, LastModifiedHeader, header))
        {
            using var store = Store(endpoint, new LogCapture());
            var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
            await using (got)
            {
                Assert.Equal("2026 年度报告", got.Metadata.Metadata["title"]);
                Assert.Equal("abc", await new StreamReader(got.Content).ReadToEndAsync());
            }
        }

        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("206 Partial Content", "ab", ETagHeader, LastModifiedHeader, header, "Content-Range: bytes 0-1/3"))
        {
            using var store = Store(endpoint, new LogCapture());
            var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), new BlobRange(0, 2), CancellationToken.None)).ValueOrThrow();
            await using (got)
            {
                Assert.Equal("2026 年度报告", got.Metadata.Metadata["title"]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(UndecodableValues))]
    public async Task OnTheWire_AHeadWithAnUndecodableEncodedWord_ReturnsStoreUnavailable(string answered)
    {
        // 不原样交出编码的文本，也不交出替换过的字符：是不合规的应答，reason 只点名请求，不带应答里的值
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader, LastModifiedHeader, "x-amz-meta-title: " + answered);
        var logs = new LogCapture();
        using var store = Store(endpoint, logs);

        var head = await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, head.Error?.Code);
        var reason = head.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.StartsWith("HeadObject ", reason, StringComparison.Ordinal);
        Assert.Contains("RFC 2047", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("?=", reason, StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("?=", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_AWholeReadWithAnUndecodableEncodedWord_ReturnsStoreUnavailable_AndReleasesTheAnswer()
    {
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", "abc", ETagHeader, LastModifiedHeader, "x-amz-meta-title: =?UTF-8?B?5Li?=");
        using var store = Store(endpoint, new LogCapture());

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, got.Error?.Code);
        Assert.StartsWith("GetObject ", got.Error!.Details?["reason"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_AnExistenceCheck_DoesNotDecodeTheMetadata()
    {
        // 只判存在与否的 HEAD 不用元数据：解不了的 encoded-word 不影响批量删除之前的判定
        await using var endpoint = RecordingHttpListener.AnsweringInTurn(
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader, "x-amz-meta-title: =?UTF-8?B?5Li?="]),
            new RecordingHttpListener.Reply("200 OK", """<?xml version="1.0" encoding="UTF-8"?><DeleteResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"></DeleteResult>""", ["Content-Type: application/xml"]));
        using var store = Store(endpoint, new LogCapture());

        var deleted = Assert.Single(await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey(Key)], CancellationToken.None));

        Assert.True(deleted.ValueOrThrow());
    }

    /// <summary>
    /// 指向回环对端、不探测条件写入的存储，日志收进 <paramref name="logs"/>
    /// </summary>
    /// <remarks>
    /// 客户端归存储所有，释放存储即释放它；缺省只尝试一次
    /// </remarks>
    private static S3BlobStore Store(RecordingHttpListener endpoint, LogCapture logs, int maxAttempts = 1)
    {
        var options = new S3ObjectStorageOptions
        {
            ServiceUrl = $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}",
            AccessKey = "ak",
            SecretKey = "sk",
            ConditionalWrites = false,
            MaxAttempts = maxAttempts,
        };
        var client = S3ObjectStorageFeature.CreateClient(options);
        return new S3BlobStore(client, options, TimeProvider.System, logs, S3ConditionalWriteGate.Unprobed(client, options, TimeProvider.System), owned: true);
    }
}

// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 别的写入方原样写进头的 UTF-8 字节读回时严格按 UTF-8 解出
/// </summary>
/// <remarks>
/// 头是 <c>x-amz-meta-*</c>（ADR-0343）。不按 RFC 2047、直接把 UTF-8 字节写进头的值，服务原样返回时 .NET 的 HTTP 客户端把每个字节读成一个 U+0000–U+00FF 的字符（Latin-1）：以前 Provider 原样交出这串乱码（含 U+0080–U+009F 的控制字符，调用方写回即被拒收）。现在还原成字节再严格按 UTF-8 解出，解不了按不合规的应答返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>。走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>（经 <see cref="RecordingHttpListener.RawHeader"/> 原样写出字节），缺省实例的传输与命名实例的传输各走一遍
/// </remarks>
public sealed class S3MetadataRawBytesWireTests
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
    /// 两条路：命名实例（过出站地址策略的传输），缺省实例（SDK 自己的传输）
    /// </summary>
    public static TheoryData<bool> Transports => [true, false];

    /// <summary>
    /// 原样写进头的值
    /// </summary>
    /// <remarks>
    /// 中文与带变音符号的拉丁字母、平面以外的字符、非 ASCII 的文字里夹着 encoded-word（整体不是 encoded-word，原样解出、不再解那一段）
    /// </remarks>
    public static TheoryData<string, bool> RawValues => new()
    {
        { "2026 年度报告", true },
        { "café Ünïcödé", true },
        { "\U0001F600 smile", true },
        { "说明 =?UTF-8?B?5Lit?=", true },
        { "2026 年度报告", false },
        { "\U0001F600 smile", false },
    };

    /// <summary>
    /// 原样写进头而解不成合法值的字节
    /// </summary>
    /// <remarks>
    /// 不是合法的 UTF-8（单独的 0xFF、截断的多字节字符、Latin-1 写的 é、编成 UTF-8 的代理项），解出 C1 控制字符 U+0085
    /// </remarks>
    public static TheoryData<byte[]> UndecodableBytes => new(
        [0xFF, 0x41],
        [0xE4, 0xB8],
        [0x63, 0x61, 0x66, 0xE9],
        [0xED, 0xA0, 0x80],
        [0x61, 0xC2, 0x85, 0x62]);

    [Theory]
    [MemberData(nameof(Transports))]
    public async Task OnTheWire_TheHttpClientHandsRawHeaderBytesAsLatin1(bool named)
    {
        // 钉住 .NET 与 SDK 的行为：每个字节一个 U+0000–U+00FF 的字符，合法与不合法的 UTF-8 都不替换、不抛出。哪天改了这条失败，还原的做法要跟着改
        byte[] valid = Encoding.UTF8.GetBytes("中文 café");
        byte[] invalid = [0xFF, 0x41, 0xE4, 0xB8];
        await using var endpoint = RecordingHttpListener.AnsweringInTurn(
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader, RecordingHttpListener.RawHeader("x-amz-meta-title", valid)]),
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader, RecordingHttpListener.RawHeader("x-amz-meta-title", invalid)]));
        using var client = Client(endpoint, named);

        var first = await client.GetObjectMetadataAsync(Bucket, Key, TestContext.Current.CancellationToken);
        var second = await client.GetObjectMetadataAsync(Bucket, Key, TestContext.Current.CancellationToken);

        Assert.Equal(Encoding.Latin1.GetString(valid), first.Metadata["title"]);
        Assert.Equal(Encoding.Latin1.GetString(invalid), second.Metadata["title"]);
    }

    [Theory]
    [MemberData(nameof(RawValues))]
    public async Task OnTheWire_AHeadWithRawUtf8Bytes_DecodesThem(string original, bool named)
    {
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader, LastModifiedHeader, RecordingHttpListener.RawHeader("x-amz-meta-title", Encoding.UTF8.GetBytes(original)), "x-amz-meta-owner: alice");
        using var store = Store(endpoint, named, new LogCapture());

        var head = (await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();

        Assert.Equal(original, head.Metadata["title"]);
        Assert.Equal("alice", head.Metadata["owner"]);
    }

    [Fact]
    public async Task OnTheWire_WholeAndRangedReads_DecodeRawUtf8Bytes()
    {
        var header = RecordingHttpListener.RawHeader("x-amz-meta-title", Encoding.UTF8.GetBytes("2026 年度报告"));
        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", "abc", ETagHeader, LastModifiedHeader, header))
        {
            using var store = Store(endpoint, named: false, new LogCapture());
            var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
            await using (got)
            {
                Assert.Equal("2026 年度报告", got.Metadata.Metadata["title"]);
                Assert.Equal("abc", await new StreamReader(got.Content).ReadToEndAsync());
            }
        }

        await using (var endpoint = RecordingHttpListener.AnsweringWithBody("206 Partial Content", "ab", ETagHeader, LastModifiedHeader, header, "Content-Range: bytes 0-1/3"))
        {
            using var store = Store(endpoint, named: false, new LogCapture());
            var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), new BlobRange(0, 2), CancellationToken.None)).ValueOrThrow();
            await using (got)
            {
                Assert.Equal("2026 年度报告", got.Metadata.Metadata["title"]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(UndecodableBytes))]
    public async Task OnTheWire_AHeadWithRawBytesThatAreNotAValidValue_ReturnsStoreUnavailable(byte[] bytes)
    {
        // 不交出 Latin-1 的乱码，也不换成 U+FFFD：是不合规的应答，reason 只点名请求，不带应答里的值，同解不了的 encoded-word
        await using var endpoint = RecordingHttpListener.Answering("200 OK", ETagHeader, LastModifiedHeader, RecordingHttpListener.RawHeader("x-amz-meta-title", bytes));
        var logs = new LogCapture();
        using var store = Store(endpoint, named: false, logs);

        var head = await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, head.Error?.Code);
        var reason = head.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Equal("HeadObject returned user metadata with raw non-ASCII bytes that do not decode as UTF-8 to a valid value", reason);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(warning.Message, static character => character > '~');
    }

    [Fact]
    public async Task OnTheWire_AWholeReadWithRawBytesThatAreNotAValidValue_ReturnsStoreUnavailable()
    {
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", "abc", ETagHeader, LastModifiedHeader, RecordingHttpListener.RawHeader("x-amz-meta-title", [0xFF, 0x41]));
        using var store = Store(endpoint, named: false, new LogCapture());

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, got.Error?.Code);
        Assert.Equal("GetObject returned user metadata with raw non-ASCII bytes that do not decode as UTF-8 to a valid value", got.Error!.Details?["reason"]);
    }

    /// <summary>
    /// 指向回环对端、只尝试一次的客户端
    /// </summary>
    /// <remarks>
    /// <paramref name="named"/> 为真时用命名实例的传输（过出站地址策略，放行回环），为假时用 SDK 自己的传输（缺省实例）
    /// </remarks>
    private static AmazonS3Client Client(RecordingHttpListener endpoint, bool named)
    {
        var options = Options(endpoint);
        return named
            ? S3ObjectStorageFeature.CreateClient(options, new S3OutboundHttpClientFactory(new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = true }, new LogCapture())))
            : S3ObjectStorageFeature.CreateClient(options);
    }

    /// <summary>
    /// 指向回环对端、不探测条件写入的存储，日志收进 <paramref name="logs"/>
    /// </summary>
    /// <remarks>
    /// 客户端归存储所有，释放存储即释放它
    /// </remarks>
    private static S3BlobStore Store(RecordingHttpListener endpoint, bool named, LogCapture logs)
    {
        var options = Options(endpoint);
        var client = Client(endpoint, named);
        return new S3BlobStore(client, options, TimeProvider.System, logs, S3ConditionalWriteGate.Unprobed(client, options, TimeProvider.System), owned: true);
    }

    /// <summary>
    /// 指向回环对端、不探测条件写入、只尝试一次的选项
    /// </summary>
    private static S3ObjectStorageOptions Options(RecordingHttpListener endpoint) => new()
    {
        ServiceUrl = $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}",
        AccessKey = "ak",
        SecretKey = "sk",
        ConditionalWrites = false,
        MaxAttempts = 1,
    };
}

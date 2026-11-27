// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Amazon.Runtime;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 整读的 ETag 不是十六进制或对不上内容时报错（ADR-0317、ADR-0322）
/// </summary>
/// <remarks>
/// AWS SDK for .NET 4.0 整读（<c>GetObject</c> 不带范围）时把不带 <c>-</c> 的 ETag 当作内容的 MD5，先按十六进制解码（<c>AWSSDKUtils.HexStringToBytes</c>，由 <c>AmazonS3ResponseHandler.ProcessResponseHandlers</c> 调用）：为空或字符数为奇数时抛 <see cref="ArgumentOutOfRangeException"/>，含十六进制数字之外的字符时抛 <see cref="FormatException"/>，都在交出应答之前。以前这两个异常原样抛给调用方；现在按不合规的应答返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，识别不靠调用栈（ADR-0322）。ETag 是十六进制而不是内容的 MD5 时，SDK 读到末尾才发现，读内容的流抛出携带该码的 <see cref="FrameworkException"/>。范围读取、HEAD 与写入没有这一核对。走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>，不连 S3 服务
/// </remarks>
public sealed class S3WholeReadETagTests
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
    /// 对端回的内容
    /// </summary>
    private const string Content = "abc";

    /// <summary>
    /// 当不了 MD5 的 ETag 头的值
    /// </summary>
    /// <remarks>
    /// 只有引号（去掉之后为空）、字符数为奇数（带引号与不带引号）、含十六进制数字之外的字符（字符数为偶数）
    /// </remarks>
    public static TheoryData<string> NonHexadecimalETags => new()
    {
        "\"\"",
        "\"abc\"",
        "abc",
        "\"zz\"",
        "\"0x12\"",
    };

    [Theory]
    [MemberData(nameof(NonHexadecimalETags))]
    public async Task OnTheWire_AWholeReadWhoseETagIsNotHexadecimal_ReturnsStoreUnavailable(string etag)
    {
        // 以前 SDK 的 ArgumentOutOfRangeException 或 FormatException 原样抛出
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: " + etag, LastModifiedHeader);
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        // reason 写明请求与 ETag 不是十六进制，不带应答里的值；SDK 与 Provider 都不重发
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, got.Error?.Code);
        var reason = (string?)got.Error!.Details!["reason"] ?? string.Empty;
        Assert.Contains("GetObject", reason, StringComparison.Ordinal);
        Assert.Contains("ETag", reason, StringComparison.Ordinal);
        Assert.Contains("hexadecimal", reason, StringComparison.Ordinal);
        Assert.DoesNotContain(etag.Trim('"') is { Length: > 0 } value ? value : "\"", reason, StringComparison.Ordinal);
        Assert.StartsWith("GET ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains("GetObject", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"zz\"")]
    [InlineData("\"abc\"")]
    public async Task OnTheWire_RangedReadsAndHeads_KeepANonHexadecimalETag(string etag)
    {
        // 范围读取与 HEAD 没有 MD5 的核对：ETag 在契约里是不透明的，Provider 不另加十六进制的要求
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("206 Partial Content", "ab", "ETag: " + etag, LastModifiedHeader, "Content-Range: bytes 0-1/3");
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var ranged = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), new BlobRange(0, 2), CancellationToken.None)).ValueOrThrow();
        await using (ranged)
        {
            Assert.Equal(etag.Trim('"'), ranged.Metadata.ETag);
        }

        Assert.Equal(etag.Trim('"'), (await store.HeadAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow().ETag);
    }

    [Theory]
    [InlineData("\"zz\"")]
    [InlineData("\"abc\"")]
    public async Task OnTheWire_AWriteWhoseETagIsNotHexadecimal_Succeeds(string etag)
    {
        // SDK 4.0 写入时不拿 ETag 核对内容（实测，ADR-0317）：写入照常，ETag 原样交出
        await using var endpoint = RecordingHttpListener.Answering("200 OK", "ETag: " + etag);
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var written = await store.PutAsync(new ContainerName(Bucket), new BlobKey(Key), Encoding.UTF8.GetBytes(Content), BlobPutOptions.Default, CancellationToken.None);

        Assert.Equal(etag.Trim('"'), written.ValueOrThrow().ETag);
        Assert.StartsWith("PUT ", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_AFormatErrorElsewhereInTheWholeRead_IsAnUnusableAnswerToo()
    {
        // 修改时间头解析失败的 FormatException 来自解组，不经过 ETag 的解码；以前原样抛出（ADR-0317 的待定），现在同是不合规的应答（ADR-0322）。
        // 不靠调用栈分不出它与含十六进制数字之外字符的 ETag，reason 同时点名两者
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: \"900150983cd24fb0d6963f7d28e17f72\"", "Last-Modified: not a date");
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var got = await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, got.Error?.Code);
        var reason = (string?)got.Error!.Details!["reason"] ?? string.Empty;
        Assert.Contains("hexadecimal", reason, StringComparison.Ordinal);
        Assert.Contains("could not parse", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("not a date", reason, StringComparison.Ordinal);
        Assert.Single(endpoint.Requests);
    }

    [Theory]
    [InlineData("\"abc\"", typeof(ArgumentOutOfRangeException))]
    [InlineData("\"\"", typeof(ArgumentOutOfRangeException))]
    [InlineData("\"zz\"", typeof(FormatException))]
    public async Task Sdk_DecodesTheWholeReadETag_ThrowingWhatTheStoreRecognisesWithoutTheStack(string etag, Type thrown)
    {
        // 钉住 Provider 识别所依据的 SDK 行为：为空或字符数为奇数时是参数名为 hex 的 ArgumentOutOfRangeException，含别的字符时是 FormatException。
        // 升级 SDK 后这里变了，识别要跟着重查
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: " + etag, LastModifiedHeader);
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () => (await client.GetObjectAsync(new GetObjectRequest { BucketName = Bucket, Key = Key })).Dispose());

        Assert.IsType(thrown, exception);
        Assert.Equal(thrown == typeof(ArgumentOutOfRangeException), S3BlobStore.IsUndecodableETag(exception));
        Assert.Equal(thrown == typeof(FormatException), S3BlobStore.IsUnparsableAnswer(exception));
    }

    [Fact]
    public void TheETagCheck_IsRecognisedWithoutAStackTrace()
    {
        // 不靠 Exception.StackTrace：裁剪掉栈追踪的运行时（Native AOT）里它是空的，识别照样成立（ADR-0322）。没抛出过的异常同样没有调用栈
        var undecodable = new ArgumentOutOfRangeException("hex");
        Assert.Null(undecodable.StackTrace);
        Assert.True(S3BlobStore.IsUndecodableETag(undecodable));

        // 别的参数名、别的异常类型都不算：不把编程错误当成服务的不合规应答
        Assert.False(S3BlobStore.IsUndecodableETag(new ArgumentOutOfRangeException("count")));
        Assert.False(S3BlobStore.IsUndecodableETag(new ArgumentException("x", "hex")));
        Assert.False(S3BlobStore.IsUndecodableETag(new FormatException()));
    }

    [Theory]
    [InlineData("async")]
    [InlineData("copy")]
    [InlineData("sync")]
    public async Task OnTheWire_AWholeReadWhoseContentDoesNotMatchItsETag_ThrowsAnObjectStorageError_WhileReading(string reading)
    {
        // ETag 是十六进制却不是内容的 MD5（兼容服务给单次写入的对象这样的 ETag，又没有 SSE 的应答头）：SDK 读到末尾才发现，
        // 元数据已经交出，流只能抛异常。以前抛 SDK 的 AmazonClientException，现在抛携带对象存储错误码的 FrameworkException（ADR-0322）
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: \"00000000000000000000000000000000\"", LastModifiedHeader);
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
        FrameworkException thrown;
        await using (got)
        {
            thrown = reading switch
            {
                "async" => await Assert.ThrowsAsync<FrameworkException>(async () => await new StreamReader(got.Content).ReadToEndAsync()),
                "copy" => await Assert.ThrowsAsync<FrameworkException>(async () => await got.Content.CopyToAsync(new MemoryStream())),
                _ => Assert.Throws<FrameworkException>(() => new StreamReader(got.Content).ReadToEnd()),
            };
        }

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, thrown.Code);
        Assert.IsType<AmazonClientException>(thrown.InnerException);
        Assert.DoesNotContain(Bucket, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("ETag", thrown.Message, StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains("GetObject", warning.Message, StringComparison.Ordinal);
        Assert.Single(endpoint.Requests);
    }

    [Fact]
    public async Task OnTheWire_AWholeReadWhoseContentDoesNotMatchItsETag_DoesNotThrow_WhenReleasedBeforeTheEnd()
    {
        // 读够了声明的长度、没读到返回 0 就释放：SDK 在释放时才核对。以前释放也抛出（ADR-0322），现在不抛、只记一条 Warning（ADR-0333）
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: \"00000000000000000000000000000000\"", LastModifiedHeader);
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
        var buffer = new byte[Content.Length];
        await got.Content.ReadExactlyAsync(buffer);
        got.Content.Dispose();
        await got.DisposeAsync();

        Assert.Equal(Content, Encoding.UTF8.GetString(buffer));
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains("released the stream before reading to its end", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_AWholeReadReleasedPartWay_NeitherThrowsNorChecks()
    {
        // 只读了一部分：SDK 不核对（已读的字节数不等于声明的长度），释放不抛、不记日志
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: \"00000000000000000000000000000000\"", LastModifiedHeader);
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
        var buffer = new byte[1];
        await got.Content.ReadExactlyAsync(buffer);
        await got.DisposeAsync();

        Assert.Equal((byte)'a', buffer[0]);
        Assert.DoesNotContain(logs.Entries, static entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task OnTheWire_AWholeReadThatReachedTheEnd_ThrowsOnceWhileReading_AndNotAgainOnRelease()
    {
        // 读到末尾的那次读抛出；SDK 核对过不再核对，之后的释放不再抛出、不再记日志
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: \"00000000000000000000000000000000\"", LastModifiedHeader);
        var logs = new LogCapture();
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, logs);

        var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
        var buffer = new byte[Content.Length];
        await got.Content.ReadExactlyAsync(buffer);
        var thrown = await Assert.ThrowsAsync<FrameworkException>(async () => await got.Content.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false));
        got.Content.Dispose();

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, thrown.Code);
        Assert.Contains("does not match", Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheWire_AWholeReadWhoseContentMatchesItsETag_ReadsThrough()
    {
        // 对照：ETag 正是内容的 MD5 时照常读完，流不改变读到的内容
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: \"900150983cd24fb0d6963f7d28e17f72\"", LastModifiedHeader);
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var got = (await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None)).ValueOrThrow();
        await using (got)
        {
            Assert.Equal(Content, await new StreamReader(got.Content).ReadToEndAsync());
        }
    }

    /// <summary>
    /// 指向回环对端、不探测条件写入的选项
    /// </summary>
    /// <remarks>
    /// 尝试次数取 3（同 SDK 的缺省）：核对 SDK 与 Provider 都不因这种应答重发
    /// </remarks>
    private static S3ObjectStorageOptions Options(int port) => new()
    {
        ServiceUrl = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
        AccessKey = "ak",
        SecretKey = "sk",
        ConditionalWrites = false,
        MaxAttempts = 3,
    };
}

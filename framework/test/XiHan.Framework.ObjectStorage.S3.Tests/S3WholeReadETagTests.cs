// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 整读的应答里 ETag 不是十六进制时返回错误（ADR-0317）
/// </summary>
/// <remarks>
/// AWS SDK for .NET 4.0 整读（<c>GetObject</c> 不带范围）时把不带 <c>-</c> 的 ETag 当作内容的 MD5，先按十六进制解码（<c>AWSSDKUtils.HexStringToBytes</c>，由 <c>AmazonS3ResponseHandler.ProcessResponseHandlers</c> 调用）：为空或字符数为奇数时抛 <see cref="ArgumentOutOfRangeException"/>，含十六进制数字之外的字符时抛 <see cref="FormatException"/>，都在交出应答之前。以前这两个异常原样抛给调用方；现在按不合规的应答返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>。范围读取、HEAD 与写入没有这一核对。走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>，不连 S3 服务
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
    /// SDK 解码 ETag 的那一帧
    /// </summary>
    /// <remarks>
    /// 识别依据：这一帧只在 SDK 后处理应答（解码整读的 ETag）时出现在异常的调用栈里
    /// </remarks>
    private const string ResponseHandlerFrame = "Amazon.S3.Internal.AmazonS3ResponseHandler.ProcessResponseHandlers";

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
    public async Task OnTheWire_AFormatErrorElsewhereInTheAnswer_IsNotTakenForTheETagCheck()
    {
        // 只接住 SDK 解码 ETag 这一处：同一个应答里修改时间头解析失败的 FormatException 来自解组，不归为 ETag 不是十六进制（不在本次范围，ADR-0317）
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: \"900150983cd24fb0d6963f7d28e17f72\"", "Last-Modified: not a date");
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));
        var store = new S3BlobStore(client, Options(endpoint.Port), TimeProvider.System, new LogCapture());

        var exception = await Assert.ThrowsAsync<FormatException>(async () => await store.GetAsync(new ContainerName(Bucket), new BlobKey(Key), CancellationToken.None));

        Assert.False(S3BlobStore.IsUndecodableETag(exception));
    }

    [Theory]
    [InlineData("\"abc\"", typeof(ArgumentOutOfRangeException))]
    [InlineData("\"\"", typeof(ArgumentOutOfRangeException))]
    [InlineData("\"zz\"", typeof(FormatException))]
    public async Task Sdk_DecodesTheWholeReadETagInItsResponseHandler(string etag, Type thrown)
    {
        // 钉住 Provider 识别所依据的 SDK 行为：升级 SDK 后这里变了，识别要跟着重查
        await using var endpoint = RecordingHttpListener.AnsweringWithBody("200 OK", Content, "ETag: " + etag, LastModifiedHeader);
        using var client = S3ObjectStorageFeature.CreateClient(Options(endpoint.Port));

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () => (await client.GetObjectAsync(new GetObjectRequest { BucketName = Bucket, Key = Key })).Dispose());

        Assert.IsType(thrown, exception);
        Assert.Contains(ResponseHandlerFrame, exception.StackTrace, StringComparison.Ordinal);
        Assert.True(S3BlobStore.IsUndecodableETag(exception));
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

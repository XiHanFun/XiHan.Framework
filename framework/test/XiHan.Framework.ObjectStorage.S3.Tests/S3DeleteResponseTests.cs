// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 批量删除的应答里的错误项（ADR-0313、ADR-0333）
/// </summary>
/// <remarks>
/// <c>DeleteObjects</c> 的错误项带着这一批里的键时只有那一项失败；错误项没有 <c>Key</c>，或带的键不在这一批里，认不出是哪一项失败：这一批里本次负责删除的项各报 <see cref="ObjectStorageErrors.StoreUnavailable"/>（同这一批的请求整个失败），不抛出；不存在的项、别的批照旧。AWS SDK for .NET 4.0 遇到有错误项的应答抛出带着应答的 <see cref="DeleteObjectsException"/>（静默模式也是）：以前 Provider 把它当作存储故障、整批报错，逐项报告只有替身走得到（ADR-0333）。用内存替身 <see cref="FakeS3Client"/>（同 SDK 抛出），以及走真实 SDK 与套接字、对端是回环上 <see cref="RecordingHttpListener"/> 的线上用例，不连 S3 服务
/// </remarks>
public sealed class S3DeleteResponseTests
{
    /// <summary>
    /// 用例的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 批量删除的请求名
    /// </summary>
    private const string DeleteRequest = "DeleteObjects";

    /// <summary>
    /// 一次 DeleteObjects 的最大键数（同 S3）
    /// </summary>
    private const int BatchSize = 1000;

    /// <summary>
    /// HEAD 应答里的 ETag 头
    /// </summary>
    private const string ETagHeader = "ETag: \"900150983cd24fb0d6963f7d28e17f72\"";

    /// <summary>
    /// HEAD 应答里的修改时间头
    /// </summary>
    private const string LastModifiedHeader = "Last-Modified: Thu, 26 Nov 2026 08:00:00 GMT";

    /// <summary>
    /// 批量删除的应答：对 <c>b.txt</c> 报 <c>AccessDenied</c>
    /// </summary>
    private const string DeniedForB = """<?xml version="1.0" encoding="UTF-8"?><DeleteResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Error><Key>b.txt</Key><Code>AccessDenied</Code><Message>Access Denied</Message></Error></DeleteResult>""";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("elsewhere.txt")]
    public async Task AnErrorItemThatNamesNoKeyOfTheBatch_FailsEveryItemTheBatchWasToDelete(string? key)
    {
        // 错误项没有键、键为空、键不在这一批里。以前以空键写字典，抛 ArgumentNullException，而这时这一批的删除已经执行了
        var fake = FakeS3Client.Create(Bucket);
        var logs = new LogCapture();
        var store = Store(fake, logs);
        await PutAsync(store, "a.txt", "b.txt");
        fake.DeleteErrors = _ => [new DeleteError { Key = key, Code = "InternalError", Message = "We encountered an internal error." }];

        var deleted = await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey("a.txt"), new BlobKey("missing.txt"), new BlobKey("b.txt"), new BlobKey("a.txt")], CancellationToken.None);

        // 存在、本次负责删除的两项都报错；不存在的与同一批里重复的照旧是"不存在"
        Assert.Equal(4, deleted.Count);
        foreach (var index in new[] { 0, 2 })
        {
            Assert.Equal(ObjectStorageErrors.StoreUnavailable, deleted[index].Error?.Code);
            var reason = deleted[index].Error!.Details?["reason"]?.ToString() ?? string.Empty;
            Assert.Contains(DeleteRequest, reason, StringComparison.Ordinal);
            Assert.Contains("Key", reason, StringComparison.Ordinal);
            Assert.DoesNotContain("InternalError", reason, StringComparison.Ordinal);
        }

        Assert.False(deleted[1].ValueOrThrow());
        Assert.False(deleted[3].ValueOrThrow());
        Assert.Single(fake.Requests, static request => request == DeleteRequest);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
        Assert.Contains(DeleteRequest, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnErrorItemForAKeyOfTheBatch_FailsOnlyThatItem()
    {
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake, new LogCapture());
        await PutAsync(store, "a.txt", "b.txt");
        fake.DeleteErrors = static _ => [new DeleteError { Key = "b.txt", Code = "AccessDenied", Message = "Access Denied" }];

        var deleted = await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey("a.txt"), new BlobKey("b.txt")], CancellationToken.None);

        Assert.True(deleted[0].ValueOrThrow());
        Assert.Equal((ObjectStorageErrors.StoreUnavailable, "AccessDenied"), (deleted[1].Error?.Code, deleted[1].Error?.Details?["reason"]?.ToString()));
        Assert.False(fake.Contains(Bucket, "a.txt"));
        Assert.True(fake.Contains(Bucket, "b.txt"));
    }

    [Fact]
    public async Task AnUnattributableErrorInOneBatch_LeavesTheOtherBatchesAlone()
    {
        // 超过 1000 个键分两批：认不出的错误项只出现在第一批的应答里，第二批照常
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake, new LogCapture());
        var names = Enumerable.Range(0, BatchSize + 1).Select(static index => string.Create(CultureInfo.InvariantCulture, $"k{index:D4}.txt")).ToArray();
        await PutAsync(store, names);
        fake.DeleteErrors = static keys => keys.Contains("k0000.txt") ? [new DeleteError { Code = "InternalError" }] : [];

        var deleted = await store.DeleteAsync(new ContainerName(Bucket), [.. names.Select(static name => new BlobKey(name))], CancellationToken.None);

        Assert.All(deleted.Take(BatchSize), static result => Assert.Equal(ObjectStorageErrors.StoreUnavailable, result.Error?.Code));
        Assert.True(deleted[BatchSize].ValueOrThrow());
        Assert.Equal(2, fake.Requests.Count(static request => request == DeleteRequest));
    }

    [Fact]
    public async Task OnTheWire_AnErrorItemForAKeyOfTheBatch_FailsOnlyThatItem()
    {
        // 真实的 SDK 遇到有错误项的应答不交出应答、抛 DeleteObjectsException：以前整批报成存储故障，reason 是那个异常类型（ADR-0333）。
        // 尝试次数取 3（同 SDK 的缺省）：核对 SDK 不因这个异常重发
        await using var endpoint = RecordingHttpListener.AnsweringInTurn(
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader]),
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader]),
            new RecordingHttpListener.Reply("200 OK", DeniedForB, ["Content-Type: application/xml"]));
        using var store = WireStore(endpoint, new LogCapture());

        var deleted = await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey("a.txt"), new BlobKey("b.txt")], CancellationToken.None);

        Assert.True(deleted[0].ValueOrThrow());
        Assert.Equal((ObjectStorageErrors.StoreUnavailable, "AccessDenied"), (deleted[1].Error?.Code, deleted[1].Error?.Details?["reason"]?.ToString()));
        Assert.Equal(3, endpoint.Requests.Count);
        Assert.StartsWith("POST ", endpoint.Requests[2], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<Error><Code>InternalError</Code></Error>")]
    [InlineData("<Error><Key>elsewhere.txt</Key><Code>InternalError</Code></Error>")]
    public async Task OnTheWire_AnErrorItemThatNamesNoKeyOfTheBatch_FailsEveryItemTheBatchWasToDelete(string error)
    {
        // 真实的 SDK 抛出的异常里带着应答：认不出是哪一项的错误项照 ADR-0313 整批报告，reason 不再是异常类型
        await using var endpoint = RecordingHttpListener.AnsweringInTurn(
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader]),
            new RecordingHttpListener.Reply("200 OK", string.Empty, [ETagHeader, LastModifiedHeader]),
            new RecordingHttpListener.Reply("200 OK", $"""<?xml version="1.0" encoding="UTF-8"?><DeleteResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">{error}</DeleteResult>""", ["Content-Type: application/xml"]));
        var logs = new LogCapture();
        using var store = WireStore(endpoint, logs);

        var deleted = await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey("a.txt"), new BlobKey("b.txt")], CancellationToken.None);

        Assert.All(deleted, static result =>
        {
            Assert.Equal(ObjectStorageErrors.StoreUnavailable, result.Error?.Code);
            var reason = result.Error!.Details?["reason"]?.ToString() ?? string.Empty;
            Assert.Contains(DeleteRequest, reason, StringComparison.Ordinal);
            Assert.Contains("Key", reason, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", reason, StringComparison.Ordinal);
        });
        Assert.Contains(DeleteRequest, Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorItemsHandedBackInTheAnswer_AreReportedTheSameWay()
    {
        // SDK 不抛出、在应答里交出错误项时（以后的 SDK 可能这样）同样逐项报告，不当作全部删成
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake, new LogCapture());
        await PutAsync(store, "a.txt", "b.txt");
        fake.DeleteErrorsAnswered = true;
        fake.DeleteErrors = static _ => [new DeleteError { Key = "b.txt", Code = "AccessDenied" }];

        var deleted = await store.DeleteAsync(new ContainerName(Bucket), [new BlobKey("a.txt"), new BlobKey("b.txt")], CancellationToken.None);

        Assert.True(deleted[0].ValueOrThrow());
        Assert.Equal((ObjectStorageErrors.StoreUnavailable, "AccessDenied"), (deleted[1].Error?.Code, deleted[1].Error?.Details?["reason"]?.ToString()));
    }

    /// <summary>
    /// 写入几个对象
    /// </summary>
    private static async Task PutAsync(S3BlobStore store, params string[] keys)
    {
        var written = await store.PutAsync(new ContainerName(Bucket), [.. keys.Select(static key => new BlobUpload(new BlobKey(key), new MemoryStream([1]), BlobPutOptions.Default))], CancellationToken.None);
        Assert.All(written, static result => Assert.True(result.IsSuccess));
    }

    /// <summary>
    /// 以替身为客户端的存储，日志收进 <paramref name="logs"/>
    /// </summary>
    private static S3BlobStore Store(FakeS3Client fake, LogCapture logs)
        => new(fake.AsClient(), new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", HealthBucket = Bucket }, TimeProvider.System, logs);

    /// <summary>
    /// 指向回环对端、不探测条件写入的存储，日志收进 <paramref name="logs"/>
    /// </summary>
    /// <remarks>
    /// 客户端归存储所有，释放存储即释放它；尝试次数取 3（同 SDK 的缺省）
    /// </remarks>
    private static S3BlobStore WireStore(RecordingHttpListener endpoint, LogCapture logs)
    {
        var options = new S3ObjectStorageOptions
        {
            ServiceUrl = $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}",
            AccessKey = "ak",
            SecretKey = "sk",
            ConditionalWrites = false,
            MaxAttempts = 3,
        };
        var client = S3ObjectStorageFeature.CreateClient(options);
        return new S3BlobStore(client, options, TimeProvider.System, logs, S3ConditionalWriteGate.Unprobed(client, options, TimeProvider.System), owned: true);
    }
}

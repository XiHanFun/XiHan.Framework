// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 放弃分片上传与建桶的应答解析不了、建桶失败时返回错误（ADR-0333）
/// </summary>
/// <remarks>
/// <c>AbortMultipartUpload</c> 与 <c>PutBucket</c> 的成功应答现在没有 SDK 按类型读的值：SDK 以后加了，解组的 <see cref="FormatException"/> / <see cref="OverflowException"/> 同其余各步按不合规的应答返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，<c>reason</c> 点名这一步。另修分片上传发起时建桶失败的异常原样抛给调用方。SDK 现在不会在这两步抛出解析异常，用内存替身 <see cref="FakeS3Client"/> 按请求名注入，不连 S3 服务
/// </remarks>
public sealed class S3AbortAndBucketAnswerTests
{
    /// <summary>
    /// 替身里的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 替身里没有的桶（<c>EnsureBuckets</c> 开启时由写入建出）
    /// </summary>
    private const string MissingBucket = "fresh";

    /// <summary>
    /// SDK 解组时的两类解析异常
    /// </summary>
    public static TheoryData<string> ParseFailures => ["format", "overflow"];

    [Theory]
    [MemberData(nameof(ParseFailures))]
    public async Task AnAbortAnswerTheSdkCannotParse_FailsThatItemOnly(string failure)
    {
        var fake = FakeS3Client.Create(Bucket);
        var logs = new LogCapture();
        var store = Store(fake, logs, ensureBuckets: false);
        var first = (await store.StartAsync(new ContainerName(Bucket), new BlobKey("a"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var second = (await store.StartAsync(new ContainerName(Bucket), new BlobKey("b"), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
        var aborts = 0;
        fake.Fault = name => name == "AbortMultipartUpload" && Interlocked.Increment(ref aborts) == 1 ? ParseFailure(failure) : null;

        var results = await store.AbortAsync([first, second], CancellationToken.None);

        AssertUnparsable(results[0].Error, "AbortMultipartUpload");
        Assert.True(results[1].IsSuccess);
        Assert.Contains("AbortMultipartUpload", Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning).Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ParseFailures))]
    public async Task ABucketAnswerTheSdkCannotParse_WhileWriting_NamesPutBucket(string failure)
    {
        // 以前由外层按写入那一步报告，reason 点名的是 PutObject
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake, new LogCapture(), ensureBuckets: true);
        fake.Fault = name => name == "PutBucket" ? ParseFailure(failure) : null;

        var put = await store.PutAsync(new ContainerName(MissingBucket), new BlobKey("a"), new byte[] { 1 }, BlobPutOptions.Default, CancellationToken.None);

        AssertUnparsable(put.Error, "PutBucket");
        Assert.Equal(["PutObject", "PutBucket"], fake.Requests);
    }

    [Theory]
    [MemberData(nameof(ParseFailures))]
    public async Task ABucketAnswerTheSdkCannotParse_WhileStartingAnUpload_NamesPutBucket(string failure)
    {
        // 以前异常原样抛给调用方：建桶在接住桶不存在的那个 catch 里，同一个 try 的其余 catch 接不住它
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake, new LogCapture(), ensureBuckets: true);
        fake.Fault = name => name == "PutBucket" ? ParseFailure(failure) : null;

        var started = await store.StartAsync(new ContainerName(MissingBucket), new BlobKey("a"), BlobPutOptions.Default, CancellationToken.None);

        AssertUnparsable(started.Error, "PutBucket");
        Assert.Equal(["InitiateMultipartUpload", "PutBucket"], fake.Requests);
    }

    [Fact]
    public async Task ABucketThatCannotBeCreated_WhileStartingAnUpload_IsAStoreFailure()
    {
        // 以前 SDK 的 AmazonS3Exception 原样抛给调用方；写入一侧一向按存储故障返回
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake, new LogCapture(), ensureBuckets: true);
        fake.Fault = static name => name == "PutBucket" ? FakeS3Client.ServiceError(HttpStatusCode.Forbidden, "AccessDenied") : null;

        var started = await store.StartAsync(new ContainerName(MissingBucket), new BlobKey("a"), BlobPutOptions.Default, CancellationToken.None);
        var put = await store.PutAsync(new ContainerName(MissingBucket), new BlobKey("a"), new byte[] { 1 }, BlobPutOptions.Default, CancellationToken.None);

        Assert.Equal((ObjectStorageErrors.StoreUnavailable, "Amazon.S3.AmazonS3Exception"), (started.Error?.Code, started.Error?.Details?["reason"]?.ToString()));
        Assert.Equal((ObjectStorageErrors.StoreUnavailable, "Amazon.S3.AmazonS3Exception"), (put.Error?.Code, put.Error?.Details?["reason"]?.ToString()));
    }

    [Fact]
    public async Task ABucketCreatedOnDemand_LetsTheUploadStart()
    {
        // 对照：建桶成功之后照旧重试一次发起
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake, new LogCapture(), ensureBuckets: true);

        var started = await store.StartAsync(new ContainerName(MissingBucket), new BlobKey("a"), BlobPutOptions.Default, CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Equal(["InitiateMultipartUpload", "PutBucket", "InitiateMultipartUpload"], fake.Requests);
    }

    /// <summary>
    /// 断言是解析不了的应答：存储不可用，<c>reason</c> 点名请求、说明解析不了
    /// </summary>
    private static void AssertUnparsable(Error? error, string operation)
    {
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, error?.Code);
        var reason = error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.StartsWith(operation + " ", reason, StringComparison.Ordinal);
        Assert.Contains("could not parse", reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// SDK 解组时会抛的解析异常，消息里带着写错的值
    /// </summary>
    private static Exception ParseFailure(string failure)
        => failure == "format" ? new FormatException("The input string 'soon' was not in a correct format.") : new OverflowException("Value was either too large or too small for an Int32.");

    /// <summary>
    /// 以替身为客户端的存储，日志收进 <paramref name="logs"/>
    /// </summary>
    /// <remarks>
    /// 不探测条件写入，用例只发起允许覆盖的上传、只做覆盖写入
    /// </remarks>
    private static S3BlobStore Store(FakeS3Client fake, LogCapture logs, bool ensureBuckets)
        => new(fake.AsClient(), new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", HealthBucket = Bucket, EnsureBuckets = ensureBuckets }, TimeProvider.System, logs);
}

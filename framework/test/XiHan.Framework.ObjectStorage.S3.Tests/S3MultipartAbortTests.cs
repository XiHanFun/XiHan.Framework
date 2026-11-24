// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 分片上传的批量放弃（ADR-0266）
/// </summary>
/// <remarks>
/// S3 没有批量放弃的接口，逐项请求：逐项结果与入参同序，一项的服务端失败只记在该项上、其余项照常请求；不存在的与重复的上传成功；每一项之前检查取消，请求途中取消照常抛出，已放弃的不恢复。用内存替身 <see cref="FakeS3Client"/> 核对发出的请求，不连服务（对真实服务的批量放弃由一致性套件的用例 14 覆盖）
/// </remarks>
public sealed class S3MultipartAbortTests
{
    /// <summary>
    /// 替身里的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 放弃分片上传的请求名
    /// </summary>
    private const string AbortRequest = "AbortMultipartUpload";

    [Fact]
    public async Task Abort_IsBatch_ReportsEachUpload_AndKeepsGoingAfterAFailure()
    {
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake);
        var first = await StartAsync(store, "a");
        var failing = await StartAsync(store, "b");
        var third = await StartAsync(store, "c");
        // 第二个放弃请求以服务端错误失败，其余照常
        var aborts = 0;
        fake.Fault = name => name == AbortRequest && Interlocked.Increment(ref aborts) == 2 ? FakeS3Client.ServiceError(HttpStatusCode.ServiceUnavailable, "SlowDown") : null;

        var results = await store.AbortAsync([first, failing, third, first], CancellationToken.None);

        // 与入参等长同序：失败只记在第二项上；第四项重复第一项，上传已不在，同样成功
        Assert.Equal([true, false, true, true], results.Select(static result => result.IsSuccess));
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, results[1].Error?.Code);
        Assert.Equal(4, fake.Requests.Count(static name => name == AbortRequest));
        Assert.Equal(1, fake.OpenUploads);

        // 失败的那一项可以单独重试
        fake.Fault = static _ => null;
        Assert.True((await store.AbortAsync(failing, CancellationToken.None)).IsSuccess);
        Assert.Equal(0, fake.OpenUploads);
    }

    [Fact]
    public async Task Abort_EmptyBatch_SendsNoRequest_AndMissingUploadsOrBucketsSucceed()
    {
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake);
        var neverStarted = new BlobMultipartUpload(new ContainerName(Bucket), new BlobKey("never"), "no-such-upload") { Overwrite = true };

        Assert.Empty(await store.AbortAsync([], CancellationToken.None));
        Assert.Empty(fake.Requests);

        // 服务回 NoSuchUpload：上传不存在或已结束，放弃是幂等的
        var missingUpload = await store.AbortAsync([neverStarted], CancellationToken.None);
        // 服务回 NoSuchBucket：桶都不在，上传自然也不在
        fake.Fault = static name => name == AbortRequest ? FakeS3Client.ServiceError(HttpStatusCode.NotFound, "NoSuchBucket") : null;
        var missingBucket = await store.AbortAsync([neverStarted], CancellationToken.None);

        Assert.True(Assert.Single(missingUpload).IsSuccess);
        Assert.True(Assert.Single(missingBucket).IsSuccess);
        Assert.Equal([AbortRequest, AbortRequest], fake.Requests);
    }

    [Fact]
    public async Task Abort_ANullUpload_ThrowsBeforeAnyRequest()
    {
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake);
        var upload = await StartAsync(store, "kept");

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.AbortAsync([upload, null!], CancellationToken.None));

        Assert.DoesNotContain(AbortRequest, fake.Requests);
        Assert.Equal(1, fake.OpenUploads);
    }

    [Fact]
    public async Task Abort_ChecksCancellationBeforeEachUpload_AndKeepsWhatWasAlreadyAborted()
    {
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake);
        var first = await StartAsync(store, "a");
        var second = await StartAsync(store, "b");
        var third = await StartAsync(store, "c");
        using var cancellation = new CancellationTokenSource();
        // 第一个放弃请求到达服务时取消：这个请求照常完成，第二个请求之前的检查抛出
        fake.Fault = name =>
        {
            if (name == AbortRequest)
            {
                cancellation.Cancel();
            }

            return null;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.AbortAsync([first, second, third], cancellation.Token));

        Assert.Equal(1, fake.Requests.Count(static name => name == AbortRequest));
        Assert.Equal(2, fake.OpenUploads);

        // 放弃是幂等的：整批重试，已放弃的那一项同样成功
        fake.Fault = static _ => null;
        var retried = await store.AbortAsync([first, second, third], CancellationToken.None);
        Assert.Equal([true, true, true], retried.Select(static result => result.IsSuccess));
        Assert.Equal(0, fake.OpenUploads);
    }

    [Fact]
    public async Task Abort_CancelledDuringARequest_Throws_InsteadOfReportingTheStoreUnavailable()
    {
        var fake = FakeS3Client.Create(Bucket);
        var store = Store(fake);
        var upload = await StartAsync(store, "hung");
        using var cancellation = new CancellationTokenSource();
        // 请求挂在服务端：调用方的取消要抛出来，不能被当成存储故障记进这一项的结果
        fake.Hang = AbortRequest;

        var aborting = store.AbortAsync([upload], cancellation.Token).AsTask();
        Assert.Contains(AbortRequest, fake.Requests);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await aborting);
        Assert.Equal(1, fake.OpenUploads);
    }

    /// <summary>
    /// 以替身为客户端的存储：不探测条件写入，用例只发起允许覆盖的上传
    /// </summary>
    private static S3BlobStore Store(FakeS3Client fake)
        => new(fake.AsClient(), new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", HealthBucket = Bucket }, TimeProvider.System, NullLoggerFactory.Instance);

    /// <summary>
    /// 发起一个允许覆盖的上传
    /// </summary>
    private static async Task<BlobMultipartUpload> StartAsync(S3BlobStore store, string key)
        => (await store.StartAsync(new ContainerName(Bucket), new BlobKey(key), BlobPutOptions.Default, CancellationToken.None)).ValueOrThrow();
}

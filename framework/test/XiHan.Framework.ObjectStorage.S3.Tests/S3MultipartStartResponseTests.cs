// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 发起分片上传的应答没有可用的上传标识时返回错误（ADR-0302）
/// </summary>
/// <remarks>
/// 服务回了成功却没给上传标识（缺失、为空，或当不了句柄）是不合规的应答：按服务端错误返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，不把句柄构造的 <see cref="ArgumentException"/> 抛给调用方。线上的一条走真实的 SDK 与套接字，对端是回环上的 <see cref="RecordingHttpListener"/>；其余用内存替身 <see cref="FakeS3Client"/>。都不连 S3 服务
/// </remarks>
public sealed class S3MultipartStartResponseTests
{
    /// <summary>
    /// 替身里的桶
    /// </summary>
    private const string Bucket = "uploads";

    /// <summary>
    /// 发起分片上传的请求名
    /// </summary>
    private const string InitiateRequest = "InitiateMultipartUpload";

    /// <summary>
    /// 句柄收得下的最长上传标识
    /// </summary>
    private const int MaxUploadIdLength = 1024;

    /// <summary>
    /// 应答里给了、却当不了句柄的上传标识
    /// </summary>
    /// <remarks>
    /// 应答里根本没有标识的情形由线上的用例与不覆盖发起的用例覆盖
    /// </remarks>
    public static TheoryData<string> UnusableUploadIds =>
    [
        string.Empty,
        "   ",
        "upload id",
        "upload\u0001id",
        new string('u', MaxUploadIdLength + 1),
    ];

    [Fact]
    public async Task OnTheWire_ASuccessfulAnswerWithoutAnUploadId_ReturnsStoreUnavailable()
    {
        // 对端回 200 而没有响应体：SDK 照收，应答里的 UploadId 为空（以前句柄的构造抛 ArgumentNullException）
        await using var endpoint = RecordingHttpListener.Answering("200 OK");
        var logs = new LogCapture();
        var options = new S3ObjectStorageOptions
        {
            ServiceUrl = $"http://127.0.0.1:{endpoint.Port.ToString(CultureInfo.InvariantCulture)}",
            AccessKey = "ak",
            SecretKey = "sk",
            ConditionalWrites = false,
            MaxAttempts = 1,
        };
        using var client = S3ObjectStorageFeature.CreateClient(options);
        var store = new S3BlobStore(client, options, TimeProvider.System, logs);

        var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey("video.mp4"), BlobPutOptions.Default, CancellationToken.None);

        // 只发了发起的那一个请求，没有重试，也没有拿不存在的标识去放弃
        Assert.Equal(ObjectStorageErrors.StoreUnavailable, started.Error?.Code);
        var request = Assert.Single(endpoint.Requests);
        Assert.StartsWith("POST ", request, StringComparison.Ordinal);
        Assert.Contains("?uploads", request, StringComparison.Ordinal);
        var warning = Assert.Single(logs.Entries, static entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Bucket, warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(UnusableUploadIds))]
    public async Task AnUnusableUploadId_ReturnsStoreUnavailable_NamingTheRequest(string uploadId)
    {
        var fake = FakeS3Client.Create(Bucket);
        fake.NextUploadId = () => uploadId;
        var store = Store(fake);

        var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey("a"), BlobPutOptions.Default, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, started.Error?.Code);
        // reason 说明是哪一步的应答缺了什么，不带应答里的值
        var reason = started.Error!.Details?["reason"]?.ToString() ?? string.Empty;
        Assert.Contains(InitiateRequest, reason, StringComparison.Ordinal);
        Assert.Contains("UploadId", reason, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(uploadId))
        {
            Assert.DoesNotContain(uploadId, reason, StringComparison.Ordinal);
        }

        Assert.Equal([InitiateRequest], fake.Requests);
    }

    [Fact]
    public async Task TheLongestUploadIdAHandleTakes_IsAccepted()
    {
        var fake = FakeS3Client.Create(Bucket);
        var longest = new string('u', MaxUploadIdLength);
        fake.NextUploadId = () => longest;
        var store = Store(fake);

        var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey("a"), BlobPutOptions.Default, CancellationToken.None);

        Assert.Equal(longest, started.ValueOrThrow().UploadId);
    }

    [Fact]
    public async Task WithoutOverwrite_TheMissingUploadId_IsReportedTheSameWay()
    {
        // 不覆盖的发起先 HEAD 再发起：缺标识照样返回错误，不因先查过而换成别的码
        var fake = FakeS3Client.Create(Bucket);
        fake.NextUploadId = static () => null;
        var store = Store(fake);

        var started = await store.StartAsync(new ContainerName(Bucket), new BlobKey("a"), BlobPutOptions.Default with { Overwrite = false }, CancellationToken.None);

        Assert.Equal(ObjectStorageErrors.StoreUnavailable, started.Error?.Code);
        Assert.Equal(["GetObjectMetadata", InitiateRequest], fake.Requests);
    }

    /// <summary>
    /// 以替身为客户端的存储
    /// </summary>
    /// <remarks>
    /// 不探测条件写入：条件写入开启即视作服务认它，不覆盖的发起照常走 HEAD 与发起
    /// </remarks>
    private static S3BlobStore Store(FakeS3Client fake)
        => new(fake.AsClient(), new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000", HealthBucket = Bucket }, TimeProvider.System, new LogCapture());
}

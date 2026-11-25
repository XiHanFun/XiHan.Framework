// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 批量删除的应答里认不出是哪一项的错误项（ADR-0313）
/// </summary>
/// <remarks>
/// <c>DeleteObjects</c> 的错误项没有 <c>Key</c>，或带的键不在这一批里，认不出是哪一项失败：这一批里本次负责删除的项各报 <see cref="ObjectStorageErrors.StoreUnavailable"/>（同这一批的请求整个失败），不抛出；不存在的项、别的批照旧。用内存替身 <see cref="FakeS3Client"/>，不连 S3 服务
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
}

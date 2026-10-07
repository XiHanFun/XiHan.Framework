// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Text;
using XiHan.Framework.ObjectStorage.Models;
using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Providers;

namespace XiHan.Framework.ObjectStorage.Tests.Providers;

/// <summary>
/// 本地分片上传并发测试
/// </summary>
public sealed class LocalFileStorageProviderChunkConcurrencyTests : IDisposable
{
    private const int PayloadLength = 16 * 1024 * 1024;
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorageProvider _provider;

    /// <summary>
    /// 初始化独立的存储根目录
    /// </summary>
    public LocalFileStorageProviderChunkConcurrencyTests()
    {
        _provider = new LocalFileStorageProvider(new OptionsWrapper<LocalStorageOptions>(new LocalStorageOptions
        {
            RootPath = _rootPath,
            UrlPrefix = "/uploads"
        }));
    }

    /// <summary>
    /// 同一会话只能有一次完成操作发布文件
    /// </summary>
    [Fact]
    public async Task CompleteChunkedUploadAsync_ConcurrentCalls_OnlyOnePublishes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploadId = await _provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest
        {
            FileName = "payload.bin",
            StoragePath = "source/payload.bin",
            TotalSize = PayloadLength,
            ChunkSize = PayloadLength
        }, cancellationToken);
        var payload = new string('A', PayloadLength);
        using var chunkStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var chunk = await _provider.UploadChunkAsync(new ChunkUploadRequest
        {
            UploadId = uploadId,
            StoragePath = "source/payload.bin",
            ChunkNumber = 1,
            ChunkData = chunkStream,
            ChunkSize = PayloadLength,
            TotalChunks = 1
        }, cancellationToken);
        Assert.True(chunk.Success, chunk.ErrorMessage);

        using var barrier = new Barrier(3);
        Task<FileUploadResult> CompleteAsync(string storagePath) => Task.Run(async () =>
        {
            barrier.SignalAndWait(cancellationToken);
            return await _provider.CompleteChunkedUploadAsync(new ChunkedUploadCompleteRequest
            {
                UploadId = uploadId,
                StoragePath = storagePath,
                ChunkInfos = [new ChunkInfo { ChunkNumber = 1, ETag = chunk.ETag }]
            }, cancellationToken);
        }, cancellationToken);

        var first = CompleteAsync("results/first.bin");
        var second = CompleteAsync("results/second.bin");
        barrier.SignalAndWait(cancellationToken);
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Success);
        Assert.Single(new[]
        {
            Path.Combine(_rootPath, "results", "first.bin"),
            Path.Combine(_rootPath, "results", "second.bin")
        }, File.Exists);
    }

    /// <summary>
    /// 取消与不完整合并交错时不会发布半成品文件
    /// </summary>
    [Fact]
    public async Task AbortDuringFailedComplete_DoesNotPublishPartialFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploadId = await _provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest
        {
            FileName = "payload.bin",
            StoragePath = "source/payload.bin",
            TotalSize = PayloadLength + 1L,
            ChunkSize = PayloadLength
        }, cancellationToken);
        var payload = new string('B', PayloadLength);
        using var chunkStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var chunk = await _provider.UploadChunkAsync(new ChunkUploadRequest
        {
            UploadId = uploadId,
            StoragePath = "source/payload.bin",
            ChunkNumber = 1,
            ChunkData = chunkStream,
            ChunkSize = PayloadLength,
            TotalChunks = 2
        }, cancellationToken);
        Assert.True(chunk.Success, chunk.ErrorMessage);

        var completeTask = _provider.CompleteChunkedUploadAsync(new ChunkedUploadCompleteRequest
        {
            UploadId = uploadId,
            StoragePath = "results/incomplete.bin",
            ChunkInfos =
            [
                new ChunkInfo { ChunkNumber = 1, ETag = chunk.ETag },
                new ChunkInfo { ChunkNumber = 2, ETag = "missing" }
            ]
        }, cancellationToken);
        await _provider.AbortChunkedUploadAsync(uploadId, cancellationToken);
        var result = await completeTask;

        Assert.False(result.Success);
        Assert.Equal("Chunk 2 not found", result.ErrorMessage);
        Assert.False(File.Exists(Path.Combine(_rootPath, "results", "incomplete.bin")));
    }

    /// <summary>
    /// 清理测试根目录
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }
}

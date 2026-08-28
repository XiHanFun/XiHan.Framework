// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.ObjectStorage.Models;

namespace XiHan.Framework.ObjectStorage.Tests.Models;

/// <summary>
/// 分片上传模型契约测试
/// </summary>
/// <remarks>
/// 分片模型是前后端分片协议的载体：默认分片大小（5MB）会被前端切片逻辑直接复用，
/// 分片序号从 1 开始、ETag 列表按序号排序合并，这些语义都由这里的默认值与往返用例守住。
/// </remarks>
public class ChunkedUploadModelsTests
{
    /// <summary>
    /// 初始化请求默认 5MB 分片、私有读、名称与路径为空串
    /// </summary>
    [Fact]
    public void ChunkedUploadInitRequest_Defaults_Use5MbPrivateChunks()
    {
        var request = new ChunkedUploadInitRequest();

        Assert.Equal(string.Empty, request.FileName);
        Assert.Equal(string.Empty, request.StoragePath);
        Assert.Equal(0L, request.TotalSize);
        Assert.Equal(5 * 1024 * 1024, request.ChunkSize);
        Assert.Null(request.ContentType);
        Assert.Null(request.BucketName);
        Assert.Equal("private", request.AccessControl);
        Assert.Null(request.Metadata);
    }

    /// <summary>
    /// 分片请求默认分片数据是空流而不是 null
    /// </summary>
    [Fact]
    public void ChunkUploadRequest_Defaults_ChunkDataIsNullStreamInstance()
    {
        var request = new ChunkUploadRequest();

        Assert.Same(Stream.Null, request.ChunkData);
        Assert.Equal(string.Empty, request.UploadId);
        Assert.Equal(string.Empty, request.StoragePath);
        Assert.Equal(0, request.ChunkNumber);
        Assert.Equal(0L, request.ChunkSize);
        Assert.Equal(0L, request.TotalSize);
        Assert.Equal(0, request.TotalChunks);
        Assert.Null(request.ChunkMd5);
        Assert.Null(request.BucketName);
    }

    /// <summary>
    /// 分片结果默认为失败态
    /// </summary>
    [Fact]
    public void ChunkUploadResult_Defaults_AreUnsuccessful()
    {
        var result = new ChunkUploadResult();

        Assert.False(result.Success);
        Assert.Equal(0, result.ChunkNumber);
        Assert.Null(result.ETag);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// 完成请求的分片列表默认是空集合而不是 null
    /// </summary>
    [Fact]
    public void ChunkedUploadCompleteRequest_Defaults_HaveEmptyChunkInfos()
    {
        var request = new ChunkedUploadCompleteRequest();

        Assert.Equal(string.Empty, request.UploadId);
        Assert.Equal(string.Empty, request.StoragePath);
        Assert.NotNull(request.ChunkInfos);
        Assert.Empty(request.ChunkInfos);
        Assert.Null(request.BucketName);
    }

    /// <summary>
    /// 分片信息默认序号为 0、ETag 为空
    /// </summary>
    [Fact]
    public void ChunkInfo_Defaults_AreZeroAndNull()
    {
        var chunkInfo = new ChunkInfo();

        Assert.Equal(0, chunkInfo.ChunkNumber);
        Assert.Null(chunkInfo.ETag);
    }

    /// <summary>
    /// 完成请求的 JSON 往返保留分片顺序与 ETag
    /// </summary>
    [Fact]
    public void ChunkedUploadCompleteRequest_JsonRoundTrip_PreservesChunkOrder()
    {
        var source = new ChunkedUploadCompleteRequest
        {
            UploadId = "3f1c",
            StoragePath = "video/a.mp4",
            BucketName = "assets",
            ChunkInfos =
            [
                new ChunkInfo { ChunkNumber = 1, ETag = "etag-1" },
                new ChunkInfo { ChunkNumber = 2, ETag = "etag-2" }
            ]
        };

        var restored = JsonSerializer.Deserialize<ChunkedUploadCompleteRequest>(JsonSerializer.Serialize(source));

        Assert.NotNull(restored);
        Assert.Equal("3f1c", restored.UploadId);
        Assert.Equal("video/a.mp4", restored.StoragePath);
        Assert.Equal("assets", restored.BucketName);
        Assert.Equal(2, restored.ChunkInfos.Count);
        Assert.Equal(1, restored.ChunkInfos[0].ChunkNumber);
        Assert.Equal("etag-1", restored.ChunkInfos[0].ETag);
        Assert.Equal(2, restored.ChunkInfos[1].ChunkNumber);
        Assert.Equal("etag-2", restored.ChunkInfos[1].ETag);
    }

    /// <summary>
    /// 初始化请求的 JSON 往返保留自定义元数据
    /// </summary>
    [Fact]
    public void ChunkedUploadInitRequest_JsonRoundTrip_PreservesMetadata()
    {
        var source = new ChunkedUploadInitRequest
        {
            FileName = "a.mp4",
            StoragePath = "video/a.mp4",
            TotalSize = 10485760L,
            ChunkSize = 1048576,
            ContentType = "video/mp4",
            AccessControl = "public-read",
            Metadata = new Dictionary<string, string> { ["owner"] = "tenant-1" }
        };

        var restored = JsonSerializer.Deserialize<ChunkedUploadInitRequest>(JsonSerializer.Serialize(source));

        Assert.NotNull(restored);
        Assert.Equal("a.mp4", restored.FileName);
        Assert.Equal("video/a.mp4", restored.StoragePath);
        Assert.Equal(10485760L, restored.TotalSize);
        Assert.Equal(1048576, restored.ChunkSize);
        Assert.Equal("video/mp4", restored.ContentType);
        Assert.Equal("public-read", restored.AccessControl);
        Assert.NotNull(restored.Metadata);
        Assert.Equal("tenant-1", restored.Metadata["owner"]);
    }

    /// <summary>
    /// 分片信息是引用语义类型，字段相同的两个实例并不相等
    /// </summary>
    [Fact]
    public void ChunkInfo_Equality_IsReferenceBased()
    {
        var left = new ChunkInfo { ChunkNumber = 1, ETag = "etag-1" };
        var right = new ChunkInfo { ChunkNumber = 1, ETag = "etag-1" };

        Assert.NotEqual(left, right);
    }
}

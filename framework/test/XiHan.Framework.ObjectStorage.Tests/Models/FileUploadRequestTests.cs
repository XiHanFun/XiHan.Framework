// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Models;

namespace XiHan.Framework.ObjectStorage.Tests.Models;

/// <summary>
/// 文件上传请求契约测试
/// </summary>
/// <remarks>
/// 三个默认值是安全底线：流默认 <see cref="Stream.Null"/>（不会 NRE）、
/// Overwrite 默认 false（不会静默覆盖线上文件）、AccessControl 默认 private（不会默认公开）。
/// </remarks>
public class FileUploadRequestTests
{
    /// <summary>
    /// 未赋值时文件流是空流而不是 null
    /// </summary>
    [Fact]
    public void Defaults_FileStreamIsNullStreamInstance()
    {
        var request = new FileUploadRequest();

        Assert.Same(Stream.Null, request.FileStream);
        Assert.Equal(0L, request.FileStream.Length);
    }

    /// <summary>
    /// 默认不覆盖、默认私有读、名称与路径为空串
    /// </summary>
    [Fact]
    public void Defaults_AreNonOverwritingAndPrivate()
    {
        var request = new FileUploadRequest();

        Assert.Equal(string.Empty, request.FileName);
        Assert.Equal(string.Empty, request.StoragePath);
        Assert.False(request.Overwrite);
        Assert.Equal("private", request.AccessControl);
    }

    /// <summary>
    /// 可选项默认全部为空
    /// </summary>
    [Fact]
    public void Defaults_OptionalMembersAreNull()
    {
        var request = new FileUploadRequest();

        Assert.Null(request.ContentType);
        Assert.Null(request.BucketName);
        Assert.Null(request.Metadata);
        Assert.Null(request.CacheControl);
        Assert.Null(request.ProgressCallback);
    }

    /// <summary>
    /// 进度回调的签名是（已传字节数，总字节数）
    /// </summary>
    [Fact]
    public void ProgressCallback_ReceivesTransferredAndTotalBytes()
    {
        long transferred = 0;
        long total = 0;
        var request = new FileUploadRequest
        {
            ProgressCallback = (sent, size) =>
            {
                transferred = sent;
                total = size;
            }
        };

        request.ProgressCallback!.Invoke(512L, 1024L);

        Assert.Equal(512L, transferred);
        Assert.Equal(1024L, total);
    }

    /// <summary>
    /// 所有属性均可读写
    /// </summary>
    [Fact]
    public void Properties_AreSettable()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var request = new FileUploadRequest
        {
            FileStream = stream,
            FileName = "a.png",
            StoragePath = "img/a.png",
            ContentType = "image/png",
            BucketName = "assets",
            Overwrite = true,
            AccessControl = "public-read",
            Metadata = new Dictionary<string, string> { ["owner"] = "tenant-1" },
            CacheControl = "max-age=3600"
        };

        Assert.Same(stream, request.FileStream);
        Assert.Equal("a.png", request.FileName);
        Assert.Equal("img/a.png", request.StoragePath);
        Assert.Equal("image/png", request.ContentType);
        Assert.Equal("assets", request.BucketName);
        Assert.True(request.Overwrite);
        Assert.Equal("public-read", request.AccessControl);
        Assert.Equal("tenant-1", request.Metadata!["owner"]);
        Assert.Equal("max-age=3600", request.CacheControl);
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Text;
using XiHan.Framework.ObjectStorage.Models;
using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Providers;

namespace XiHan.Framework.ObjectStorage.Tests.Providers;

/// <summary>
/// 本地文件存储提供程序测试
/// </summary>
/// <remarks>
/// 本地 Provider 是唯一不依赖云端就能跑完整闭环的实现，所以这里做真实磁盘读写：
/// 每个用例在系统临时目录下开一个独立根目录（xUnit 每个测试方法都会新建测试类实例，构造函数天然隔离），
/// Dispose 里递归删除并吞掉占用异常，避免清理失败反过来把用例判红。
/// 关注点集中在三处易错逻辑：URL 前缀与落盘路径的互相剥离、覆盖策略、分片合并闭环。
/// </remarks>
public sealed class LocalFileStorageProviderTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorageProvider _provider;

    /// <summary>
    /// 构造函数：为当前用例准备独占的临时根目录
    /// </summary>
    public LocalFileStorageProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _provider = CreateProvider("/uploads");
    }

    /// <summary>
    /// 提供程序名称与能力声明固定
    /// </summary>
    [Fact]
    public void ProviderName_AndCapabilities_AreStable()
    {
        Assert.Equal("Local", _provider.ProviderName);
        Assert.True(_provider.SupportChunkedUpload);
        Assert.True(_provider.SupportResumableUpload);
    }

    /// <summary>
    /// 根目录不存在时构造函数负责创建
    /// </summary>
    [Fact]
    public void Constructor_WhenRootDirectoryMissing_CreatesIt()
    {
        var rootPath = Path.Combine(_root, Guid.NewGuid().ToString("N"), "nested");
        Assert.False(Directory.Exists(rootPath));

        _ = new LocalFileStorageProvider(new OptionsWrapper<LocalStorageOptions>(new LocalStorageOptions
        {
            RootPath = rootPath,
            UrlPrefix = "/uploads"
        }));

        Assert.True(Directory.Exists(rootPath));
    }

    /// <summary>
    /// 上传成功时落盘并返回路径、大小与内容哈希
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenSucceeds_WritesFileAndReturnsMetadata()
    {
        var result = await UploadTextAsync(_provider, "docs/a.txt", "hello");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("docs/a.txt", result.Path);
        Assert.Equal("/uploads/docs/a.txt", result.Url);
        Assert.Equal(5L, result.FileSize);
        // MD5("hello") 的标准取值，同时验证「小写十六进制」这一格式约定
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", result.ETag);
        Assert.True(result.DurationMs >= 0L);
        Assert.NotNull(result.FullPath);
        Assert.True(File.Exists(result.FullPath));
        Assert.Equal("hello", await ReadAllTextAsync(_provider, "docs/a.txt"));
    }

    /// <summary>
    /// 目标文件已存在且未允许覆盖时返回失败且不动原文件
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenFileExistsAndOverwriteDisabled_FailsWithoutTouchingOriginal()
    {
        await UploadTextAsync(_provider, "docs/dup.txt", "first");

        var result = await UploadTextAsync(_provider, "docs/dup.txt", "second", overwrite: false);

        Assert.False(result.Success);
        Assert.Equal("File already exists", result.ErrorMessage);
        Assert.Null(result.Path);
        Assert.Equal("first", await ReadAllTextAsync(_provider, "docs/dup.txt"));
    }

    /// <summary>
    /// 允许覆盖时用新内容整体替换旧文件
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenOverwriteEnabled_ReplacesContent()
    {
        await UploadTextAsync(_provider, "docs/dup.txt", "a-much-longer-first-content");

        var result = await UploadTextAsync(_provider, "docs/dup.txt", "second", overwrite: true);

        Assert.True(result.Success);
        Assert.Equal(6L, result.FileSize);
        Assert.Equal("second", await ReadAllTextAsync(_provider, "docs/dup.txt"));
    }

    /// <summary>
    /// 上传过程按已传字节数回调进度，最终一次回调等于文件总大小
    /// </summary>
    [Fact]
    public async Task UploadAsync_WithProgressCallback_ReportsTransferredBytes()
    {
        var reported = new List<(long Transferred, long Total)>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        var request = new FileUploadRequest
        {
            FileStream = stream,
            FileName = "p.txt",
            StoragePath = "docs/p.txt",
            Overwrite = true,
            ProgressCallback = (transferred, total) => reported.Add((transferred, total))
        };

        var result = await _provider.UploadAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotEmpty(reported);
        Assert.Equal(5L, reported[^1].Transferred);
        Assert.Equal(5L, reported[^1].Total);
    }

    /// <summary>
    /// 存储路径已经带上 URL 前缀时不会被二次拼接
    /// </summary>
    /// <remarks>
    /// 上层常常把 GetMetadata 返回的 Url 直接回灌成 StoragePath，
    /// Provider 会先剥掉前缀段再落盘，因此 URL 里前缀只出现一次、文件也不会多一层 uploads 目录。
    /// </remarks>
    [Fact]
    public async Task UploadAsync_WhenStoragePathCarriesUrlPrefix_StripsPrefixBeforeWriting()
    {
        var result = await UploadTextAsync(_provider, "uploads/docs/a.txt", "hello");

        Assert.True(result.Success);
        Assert.Equal("uploads/docs/a.txt", result.Path);
        Assert.Equal("/uploads/docs/a.txt", result.Url);
        Assert.True(await _provider.ExistsAsync("docs/a.txt", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 反斜杠路径与前导斜杠都会被规范化到同一个物理文件
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithBackslashOrLeadingSlash_ResolvesSameFile()
    {
        await UploadTextAsync(_provider, "docs/sub/a.txt", "hello");
        var token = TestContext.Current.CancellationToken;

        Assert.True(await _provider.ExistsAsync("docs/sub/a.txt", token));
        Assert.True(await _provider.ExistsAsync("docs\\sub\\a.txt", token));
        Assert.True(await _provider.ExistsAsync("/docs/sub/a.txt", token));
    }

    /// <summary>
    /// 文件不存在时存在性检查返回 false
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WhenFileMissing_ReturnsFalse()
    {
        Assert.False(await _provider.ExistsAsync("docs/missing.txt", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 带桶名的存在性检查在本地存储上等价于不带桶名
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithBucketName_IgnoresBucketAndUsesRootPath()
    {
        await UploadTextAsync(_provider, "docs/a.txt", "hello");

        Assert.True(await _provider.ExistsAsync("docs/a.txt", "any-bucket", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 下载不存在的文件抛 FileNotFoundException 并带上相对路径
    /// </summary>
    [Fact]
    public async Task DownloadAsync_WhenFileMissing_ThrowsFileNotFound()
    {
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _provider.DownloadAsync("docs/missing.txt", TestContext.Current.CancellationToken));

        Assert.Contains("docs/missing.txt", exception.Message);
    }

    /// <summary>
    /// 删除后文件不再存在
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenFileExists_RemovesIt()
    {
        var token = TestContext.Current.CancellationToken;
        await UploadTextAsync(_provider, "docs/a.txt", "hello");

        await _provider.DeleteAsync("docs/a.txt", token);

        Assert.False(await _provider.ExistsAsync("docs/a.txt", token));
    }

    /// <summary>
    /// 删除不存在的文件是幂等的、不抛异常
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WhenFileMissing_IsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;

        await _provider.DeleteAsync("docs/missing.txt", token);
        await _provider.DeleteAsync("docs/missing.txt", "any-bucket", token);

        Assert.False(await _provider.ExistsAsync("docs/missing.txt", token));
    }

    /// <summary>
    /// 元数据返回文件名、大小、按扩展名推断的内容类型与直链
    /// </summary>
    [Fact]
    public async Task GetMetadataAsync_WhenFileExists_ReturnsFileFacts()
    {
        var token = TestContext.Current.CancellationToken;
        await UploadTextAsync(_provider, "img/a.png", "hello");

        var metadata = await _provider.GetMetadataAsync("img/a.png", token);

        Assert.Equal("a.png", metadata.Name);
        Assert.Equal("img/a.png", metadata.Path);
        Assert.Equal(5L, metadata.Size);
        Assert.Equal("image/png", metadata.ContentType);
        Assert.False(metadata.IsDirectory);
        Assert.Equal("/uploads/img/a.png", metadata.Url);
        Assert.NotNull(metadata.LastModified);
    }

    /// <summary>
    /// 未识别的扩展名回落到通用二进制类型
    /// </summary>
    [Theory]
    [InlineData("a.jpg", "image/jpeg")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.pdf", "application/pdf")]
    [InlineData("a.zip", "application/zip")]
    [InlineData("a.mp4", "video/mp4")]
    [InlineData("a.mp3", "audio/mpeg")]
    [InlineData("a.txt", "application/octet-stream")]
    [InlineData("a.PNG", "image/png")]
    public async Task GetMetadataAsync_InfersContentTypeFromExtension(string fileName, string expectedContentType)
    {
        var token = TestContext.Current.CancellationToken;
        var storagePath = $"types/{fileName}";
        await UploadTextAsync(_provider, storagePath, "hello");

        var metadata = await _provider.GetMetadataAsync(storagePath, token);

        Assert.Equal(expectedContentType, metadata.ContentType);
    }

    /// <summary>
    /// 获取不存在文件的元数据抛 FileNotFoundException
    /// </summary>
    [Fact]
    public async Task GetMetadataAsync_WhenFileMissing_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _provider.GetMetadataAsync("docs/missing.txt", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 复制会按需建目录且保留源文件
    /// </summary>
    [Fact]
    public async Task CopyAsync_WhenSourceExists_CopiesAndKeepsSource()
    {
        var token = TestContext.Current.CancellationToken;
        await UploadTextAsync(_provider, "docs/src.txt", "hello");

        await _provider.CopyAsync("docs/src.txt", "backup/nested/dst.txt", token);

        Assert.True(await _provider.ExistsAsync("docs/src.txt", token));
        Assert.True(await _provider.ExistsAsync("backup/nested/dst.txt", token));
        Assert.Equal("hello", await ReadAllTextAsync(_provider, "backup/nested/dst.txt"));
    }

    /// <summary>
    /// 复制到已存在的目标会覆盖
    /// </summary>
    [Fact]
    public async Task CopyAsync_WhenDestinationExists_Overwrites()
    {
        var token = TestContext.Current.CancellationToken;
        await UploadTextAsync(_provider, "docs/src.txt", "hello");
        await UploadTextAsync(_provider, "docs/dst.txt", "old-content");

        await _provider.CopyAsync("docs/src.txt", "docs/dst.txt", token);

        Assert.Equal("hello", await ReadAllTextAsync(_provider, "docs/dst.txt"));
    }

    /// <summary>
    /// 源文件不存在时复制抛 FileNotFoundException
    /// </summary>
    [Fact]
    public async Task CopyAsync_WhenSourceMissing_ThrowsFileNotFound()
    {
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _provider.CopyAsync("docs/missing.txt", "docs/dst.txt", TestContext.Current.CancellationToken));

        Assert.Contains("docs/missing.txt", exception.Message);
    }

    /// <summary>
    /// 移动会按需建目录并删除源文件
    /// </summary>
    [Fact]
    public async Task MoveAsync_WhenSourceExists_MovesAndRemovesSource()
    {
        var token = TestContext.Current.CancellationToken;
        await UploadTextAsync(_provider, "docs/src.txt", "hello");

        await _provider.MoveAsync("docs/src.txt", "archive/nested/dst.txt", token);

        Assert.False(await _provider.ExistsAsync("docs/src.txt", token));
        Assert.True(await _provider.ExistsAsync("archive/nested/dst.txt", token));
        Assert.Equal("hello", await ReadAllTextAsync(_provider, "archive/nested/dst.txt"));
    }

    /// <summary>
    /// 源文件不存在时移动抛 FileNotFoundException
    /// </summary>
    [Fact]
    public async Task MoveAsync_WhenSourceMissing_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _provider.MoveAsync("docs/missing.txt", "docs/dst.txt", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 列目录默认只列当前层，递归时才带出子目录文件
    /// </summary>
    [Fact]
    public async Task ListFilesAsync_RespectsRecursiveFlag()
    {
        var token = TestContext.Current.CancellationToken;
        var provider = CreateProvider("/uploads");
        await UploadTextAsync(provider, "root.txt", "r");
        await UploadTextAsync(provider, "sub/child.txt", "c");

        var topLevel = await provider.ListFilesAsync(string.Empty, false, token);
        var recursive = await provider.ListFilesAsync(string.Empty, true, token);

        Assert.Single(topLevel);
        Assert.Equal("root.txt", topLevel[0].Path);
        Assert.Equal(2, recursive.Count);
        Assert.Contains(recursive, item => item.Path == "sub/child.txt");
    }

    /// <summary>
    /// 列出的条目携带相对路径、大小与直链
    /// </summary>
    [Fact]
    public async Task ListFilesAsync_ReturnsRelativePathAndUrl()
    {
        var token = TestContext.Current.CancellationToken;
        var provider = CreateProvider("/uploads");
        await UploadTextAsync(provider, "sub/child.txt", "hello");

        var files = await provider.ListFilesAsync("sub", false, token);

        Assert.Single(files);
        Assert.Equal("child.txt", files[0].Name);
        Assert.Equal("sub/child.txt", files[0].Path);
        Assert.Equal(5L, files[0].Size);
        Assert.False(files[0].IsDirectory);
        Assert.Equal("/uploads/sub/child.txt", files[0].Url);
    }

    /// <summary>
    /// 目录不存在时列目录返回空集合而不是抛异常
    /// </summary>
    [Fact]
    public async Task ListFilesAsync_WhenDirectoryMissing_ReturnsEmptyList()
    {
        var files = await _provider.ListFilesAsync("no-such-dir", true, TestContext.Current.CancellationToken);

        Assert.NotNull(files);
        Assert.Empty(files);
    }

    /// <summary>
    /// 预签名 URL 在本地存储上退化为静态直链，且与过期时间无关
    /// </summary>
    /// <remarks>
    /// 本地存储没有签名机制，这里锁死「忽略 expiresIn、返回可直接静态访问的 URL」这一有意为之的降级语义，
    /// 调用方需要时效控制时必须换用云存储 Provider。
    /// </remarks>
    [Fact]
    public async Task GeneratePresignedUrlAsync_IgnoresExpiryAndReturnsStaticUrl()
    {
        var token = TestContext.Current.CancellationToken;

        var shortLived = await _provider.GeneratePresignedUrlAsync("img/a.png", TimeSpan.FromMinutes(1), token);
        var longLived = await _provider.GeneratePresignedUrlAsync("img/a.png", TimeSpan.FromDays(30), token);

        Assert.Equal("/uploads/img/a.png", shortLived);
        Assert.Equal(shortLived, longLived);
    }

    /// <summary>
    /// URL 前缀的各种写法都被规范化成同一种直链形式
    /// </summary>
    [Theory]
    [InlineData("/uploads", "docs/a.txt", "/uploads/docs/a.txt")]
    [InlineData("uploads", "docs/a.txt", "/uploads/docs/a.txt")]
    [InlineData("/uploads/", "docs/a.txt", "/uploads/docs/a.txt")]
    [InlineData("/", "docs/a.txt", "/docs/a.txt")]
    [InlineData("", "docs/a.txt", "/docs/a.txt")]
    [InlineData("/files", "\\docs\\a.txt", "/files/docs/a.txt")]
    public async Task GeneratePresignedUrlAsync_NormalizesUrlPrefix(string urlPrefix, string storagePath, string expectedUrl)
    {
        var provider = CreateProvider(urlPrefix);

        var url = await provider.GeneratePresignedUrlAsync(storagePath, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        Assert.Equal(expectedUrl, url);
    }

    /// <summary>
    /// 分片上传全流程：初始化、乱序上传分片、按序号合并成完整文件
    /// </summary>
    [Fact]
    public async Task ChunkedUpload_FullCycle_MergesChunksInChunkNumberOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var uploadId = await _provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest
        {
            FileName = "a.bin",
            StoragePath = "video/a.bin",
            TotalSize = 7L,
            ChunkSize = 4
        }, token);

        Assert.False(string.IsNullOrWhiteSpace(uploadId));

        // 故意先传第 2 片再传第 1 片，验证合并只认 ChunkNumber、不认上传顺序
        var second = await UploadChunkAsync(_provider, uploadId, 2, "BBBB");
        var first = await UploadChunkAsync(_provider, uploadId, 1, "AAA");

        Assert.True(first.Success, first.ErrorMessage);
        Assert.Equal(1, first.ChunkNumber);
        Assert.NotNull(first.ETag);
        Assert.Equal(32, first.ETag!.Length);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.Equal(2, second.ChunkNumber);

        var result = await _provider.CompleteChunkedUploadAsync(new ChunkedUploadCompleteRequest
        {
            UploadId = uploadId,
            StoragePath = "video/a.bin",
            ChunkInfos =
            [
                new ChunkInfo { ChunkNumber = 2, ETag = second.ETag },
                new ChunkInfo { ChunkNumber = 1, ETag = first.ETag }
            ]
        }, token);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("video/a.bin", result.Path);
        Assert.Equal("/uploads/video/a.bin", result.Url);
        Assert.Equal(7L, result.FileSize);
        Assert.NotNull(result.ETag);
        Assert.Equal(32, result.ETag!.Length);
        Assert.Equal("AAABBBB", await ReadAllTextAsync(_provider, "video/a.bin"));
    }

    /// <summary>
    /// 完成分片上传后会话被清理，重复完成会被判为会话不存在
    /// </summary>
    [Fact]
    public async Task CompleteChunkedUploadAsync_CalledTwice_SecondCallReportsSessionMissing()
    {
        var token = TestContext.Current.CancellationToken;
        var uploadId = await _provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest
        {
            FileName = "a.bin",
            StoragePath = "video/b.bin",
            TotalSize = 3L,
            ChunkSize = 3
        }, token);
        var chunk = await UploadChunkAsync(_provider, uploadId, 1, "AAA");

        var completeRequest = new ChunkedUploadCompleteRequest
        {
            UploadId = uploadId,
            StoragePath = "video/b.bin",
            ChunkInfos = [new ChunkInfo { ChunkNumber = 1, ETag = chunk.ETag }]
        };

        var firstComplete = await _provider.CompleteChunkedUploadAsync(completeRequest, token);
        var secondComplete = await _provider.CompleteChunkedUploadAsync(completeRequest, token);

        Assert.True(firstComplete.Success);
        Assert.False(secondComplete.Success);
        Assert.Equal("Upload session not found", secondComplete.ErrorMessage);
    }

    /// <summary>
    /// 上传ID 不存在时上传分片返回失败而不是抛异常
    /// </summary>
    [Fact]
    public async Task UploadChunkAsync_WhenSessionUnknown_ReturnsFailure()
    {
        var result = await UploadChunkAsync(_provider, "not-an-upload-id", 3, "AAA");

        Assert.False(result.Success);
        Assert.Equal(3, result.ChunkNumber);
        Assert.Equal("Upload session not found", result.ErrorMessage);
        Assert.Null(result.ETag);
    }

    /// <summary>
    /// 上传ID 不存在时完成分片上传返回失败而不是抛异常
    /// </summary>
    [Fact]
    public async Task CompleteChunkedUploadAsync_WhenSessionUnknown_ReturnsFailure()
    {
        var result = await _provider.CompleteChunkedUploadAsync(new ChunkedUploadCompleteRequest
        {
            UploadId = "not-an-upload-id",
            StoragePath = "video/c.bin"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Upload session not found", result.ErrorMessage);
    }

    /// <summary>
    /// 声明的分片缺失时完成分片上传报出缺失的分片序号
    /// </summary>
    [Fact]
    public async Task CompleteChunkedUploadAsync_WhenChunkMissing_ReportsMissingChunkNumber()
    {
        var token = TestContext.Current.CancellationToken;
        var uploadId = await _provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest
        {
            FileName = "a.bin",
            StoragePath = "video/d.bin",
            TotalSize = 7L,
            ChunkSize = 4
        }, token);
        var chunk = await UploadChunkAsync(_provider, uploadId, 1, "AAA");

        var result = await _provider.CompleteChunkedUploadAsync(new ChunkedUploadCompleteRequest
        {
            UploadId = uploadId,
            StoragePath = "video/d.bin",
            ChunkInfos =
            [
                new ChunkInfo { ChunkNumber = 1, ETag = chunk.ETag },
                new ChunkInfo { ChunkNumber = 2, ETag = "missing-etag" }
            ]
        }, token);

        Assert.False(result.Success);
        Assert.Equal("Chunk 2 not found", result.ErrorMessage);

        // 失败分支不会清理会话，这里手动收尾，避免临时分片目录残留
        await _provider.AbortChunkedUploadAsync(uploadId, token);
    }

    /// <summary>
    /// 取消分片上传会清掉会话，之后再传分片按会话不存在处理
    /// </summary>
    [Fact]
    public async Task AbortChunkedUploadAsync_DropsSession()
    {
        var token = TestContext.Current.CancellationToken;
        var uploadId = await _provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest
        {
            FileName = "a.bin",
            StoragePath = "video/e.bin",
            TotalSize = 3L,
            ChunkSize = 3
        }, token);

        await _provider.AbortChunkedUploadAsync(uploadId, token);
        var afterAbort = await UploadChunkAsync(_provider, uploadId, 1, "AAA");

        Assert.False(afterAbort.Success);
        Assert.Equal("Upload session not found", afterAbort.ErrorMessage);
    }

    /// <summary>
    /// 取消不存在的上传ID 是幂等的、不抛异常
    /// </summary>
    [Fact]
    public async Task AbortChunkedUploadAsync_WhenSessionUnknown_IsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;

        await _provider.AbortChunkedUploadAsync("not-an-upload-id", token);
        await _provider.AbortChunkedUploadAsync("not-an-upload-id", token);

        var result = await UploadChunkAsync(_provider, "not-an-upload-id", 1, "AAA");
        Assert.False(result.Success);
    }

    /// <summary>
    /// 每次初始化分片上传都会拿到互不相同的上传ID
    /// </summary>
    [Fact]
    public async Task InitiateChunkedUploadAsync_ReturnsDistinctUploadIds()
    {
        var token = TestContext.Current.CancellationToken;
        var request = new ChunkedUploadInitRequest
        {
            FileName = "a.bin",
            StoragePath = "video/f.bin",
            TotalSize = 3L,
            ChunkSize = 3
        };

        var first = await _provider.InitiateChunkedUploadAsync(request, token);
        var second = await _provider.InitiateChunkedUploadAsync(request, token);

        Assert.NotEqual(first, second);

        await _provider.AbortChunkedUploadAsync(first, token);
        await _provider.AbortChunkedUploadAsync(second, token);
    }

    /// <summary>
    /// 清理当前用例的临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch
        {
            // 文件被占用或已被清理都不应影响用例结论，忽略
        }
    }

    /// <summary>
    /// 在当前用例的临时根目录下新建一个独立的本地存储提供程序
    /// </summary>
    private LocalFileStorageProvider CreateProvider(string urlPrefix)
    {
        var options = new LocalStorageOptions
        {
            RootPath = Path.Combine(_root, Guid.NewGuid().ToString("N")),
            UrlPrefix = urlPrefix
        };

        return new LocalFileStorageProvider(new OptionsWrapper<LocalStorageOptions>(options));
    }

    /// <summary>
    /// 以文本内容上传一个文件
    /// </summary>
    private static async Task<FileUploadResult> UploadTextAsync(
        LocalFileStorageProvider provider,
        string storagePath,
        string content,
        bool overwrite = true)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var request = new FileUploadRequest
        {
            FileStream = stream,
            FileName = Path.GetFileName(storagePath),
            StoragePath = storagePath,
            Overwrite = overwrite
        };

        return await provider.UploadAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 上传一个文本分片
    /// </summary>
    private static async Task<ChunkUploadResult> UploadChunkAsync(
        LocalFileStorageProvider provider,
        string uploadId,
        int chunkNumber,
        string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        using var stream = new MemoryStream(bytes);
        var request = new ChunkUploadRequest
        {
            UploadId = uploadId,
            StoragePath = "video/a.bin",
            ChunkNumber = chunkNumber,
            ChunkData = stream,
            ChunkSize = bytes.Length,
            TotalChunks = 2
        };

        return await provider.UploadChunkAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 下载并读出文本内容
    /// </summary>
    private static async Task<string> ReadAllTextAsync(LocalFileStorageProvider provider, string storagePath)
    {
        var token = TestContext.Current.CancellationToken;
        await using var stream = await provider.DownloadAsync(storagePath, token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(token);
    }
}

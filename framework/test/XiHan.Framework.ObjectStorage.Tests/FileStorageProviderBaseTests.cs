// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.ObjectStorage.Models;
using XiHan.Framework.ObjectStorage.Tests.Fakes;

namespace XiHan.Framework.ObjectStorage.Tests;

/// <summary>
/// 文件存储提供程序抽象基类测试
/// </summary>
/// <remarks>
/// 基类是所有 Provider 的模板：UploadAsync 负责计时并把异常吞成失败结果，
/// 带桶名的三个重载默认忽略桶名转调单桶实现，分片与预签名默认不支持直接抛 NotSupportedException。
/// 这些是子类可以依赖的默认行为，用一个最小具体子类把它们逐条钉死。
/// </remarks>
public class FileStorageProviderBaseTests
{
    /// <summary>
    /// 未覆写时默认既不支持分片也不支持断点续传
    /// </summary>
    [Fact]
    public void SupportFlags_WhenNotOverridden_AreFalse()
    {
        var provider = new RecordingFileStorageProvider();

        Assert.False(provider.SupportChunkedUpload);
        Assert.False(provider.SupportResumableUpload);
    }

    /// <summary>
    /// 上传成功时原样返回核心实现的结果并补上耗时
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenCoreSucceeds_ReturnsCoreResultWithDuration()
    {
        var provider = new RecordingFileStorageProvider();
        var coreResult = new FileUploadResult
        {
            Success = true,
            Path = "img/a.png",
            FileSize = 12L
        };
        provider.UploadCoreResult = coreResult;

        var result = await provider.UploadAsync(new FileUploadRequest(), TestContext.Current.CancellationToken);

        Assert.Same(coreResult, result);
        Assert.True(result.Success);
        Assert.Equal("img/a.png", result.Path);
        Assert.True(result.DurationMs >= 0L);
        Assert.Equal(1, provider.UploadCoreCallCount);
    }

    /// <summary>
    /// 核心实现抛异常时转成失败结果而不是向外冒泡
    /// </summary>
    [Fact]
    public async Task UploadAsync_WhenCoreThrows_ReturnsFailureWithExceptionMessage()
    {
        var provider = new RecordingFileStorageProvider
        {
            UploadCoreException = new InvalidOperationException("磁盘写入失败")
        };

        var result = await provider.UploadAsync(new FileUploadRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("磁盘写入失败", result.ErrorMessage);
        Assert.Null(result.Path);
        Assert.Null(result.Url);
        Assert.True(result.DurationMs >= 0L);
    }

    /// <summary>
    /// 未覆写分片能力时初始化分片上传抛 NotSupportedException 并点名 Provider
    /// </summary>
    [Fact]
    public async Task InitiateChunkedUploadAsync_WhenNotOverridden_ThrowsNotSupported()
    {
        var provider = new RecordingFileStorageProvider();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest(), TestContext.Current.CancellationToken));

        Assert.Contains(provider.ProviderName, exception.Message);
    }

    /// <summary>
    /// 未覆写分片能力时上传分片抛 NotSupportedException
    /// </summary>
    [Fact]
    public async Task UploadChunkAsync_WhenNotOverridden_ThrowsNotSupported()
    {
        var provider = new RecordingFileStorageProvider();

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await provider.UploadChunkAsync(new ChunkUploadRequest(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 未覆写分片能力时完成分片上传抛 NotSupportedException
    /// </summary>
    [Fact]
    public async Task CompleteChunkedUploadAsync_WhenNotOverridden_ThrowsNotSupported()
    {
        var provider = new RecordingFileStorageProvider();

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await provider.CompleteChunkedUploadAsync(new ChunkedUploadCompleteRequest(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 未覆写分片能力时取消分片上传抛 NotSupportedException
    /// </summary>
    [Fact]
    public async Task AbortChunkedUploadAsync_WhenNotOverridden_ThrowsNotSupported()
    {
        var provider = new RecordingFileStorageProvider();

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await provider.AbortChunkedUploadAsync("upload-1", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 未覆写预签名能力时抛 NotSupportedException 并点名 Provider
    /// </summary>
    [Fact]
    public async Task GeneratePresignedUrlAsync_WhenNotOverridden_ThrowsNotSupported()
    {
        var provider = new RecordingFileStorageProvider();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await provider.GeneratePresignedUrlAsync("img/a.png", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));

        Assert.Contains(provider.ProviderName, exception.Message);
    }

    /// <summary>
    /// 带桶名的删除默认忽略桶名、转调单桶实现
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithBucketName_DelegatesToSingleBucketOverload()
    {
        var provider = new RecordingFileStorageProvider();

        await provider.DeleteAsync("img/a.png", "assets", TestContext.Current.CancellationToken);

        Assert.Single(provider.DeletedPaths);
        Assert.Equal("img/a.png", provider.DeletedPaths[0]);
    }

    /// <summary>
    /// 带桶名的存在性检查默认忽略桶名、转调单桶实现
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithBucketName_DelegatesToSingleBucketOverload()
    {
        var provider = new RecordingFileStorageProvider { ExistsResult = false };

        var exists = await provider.ExistsAsync("img/a.png", "assets", TestContext.Current.CancellationToken);

        Assert.False(exists);
        Assert.Single(provider.ExistsPaths);
        Assert.Equal("img/a.png", provider.ExistsPaths[0]);
    }

    /// <summary>
    /// 带桶名的元数据获取默认忽略桶名、转调单桶实现
    /// </summary>
    [Fact]
    public async Task GetMetadataAsync_WithBucketName_DelegatesToSingleBucketOverload()
    {
        var provider = new RecordingFileStorageProvider();

        var metadata = await provider.GetMetadataAsync("img/a.png", "assets", TestContext.Current.CancellationToken);

        Assert.Equal("img/a.png", metadata.Path);
        Assert.Single(provider.MetadataPaths);
        Assert.Equal("img/a.png", provider.MetadataPaths[0]);
    }

    /// <summary>
    /// 桶名为空时同样走单桶实现
    /// </summary>
    [Fact]
    public async Task ExistsAsync_WithNullBucketName_StillDelegates()
    {
        var provider = new RecordingFileStorageProvider();

        var exists = await provider.ExistsAsync("img/a.png", null, TestContext.Current.CancellationToken);

        Assert.True(exists);
        Assert.Single(provider.ExistsPaths);
    }

    /// <summary>
    /// 路径规范化统一反斜杠并去掉开头的斜杠
    /// </summary>
    [Theory]
    [InlineData("img/a.png", "img/a.png")]
    [InlineData("img\\a.png", "img/a.png")]
    [InlineData("/img/a.png", "img/a.png")]
    [InlineData("///img/a.png", "img/a.png")]
    [InlineData("\\img\\sub\\a.png", "img/sub/a.png")]
    [InlineData("", "")]
    public void NormalizePath_UnifiesSeparatorsAndTrimsLeadingSlash(string path, string expected)
    {
        var provider = new RecordingFileStorageProvider();

        Assert.Equal(expected, provider.CallNormalizePath(path));
    }

    /// <summary>
    /// 扩展名提取去掉前导点，无扩展名时返回空串
    /// </summary>
    [Theory]
    [InlineData("a.png", "png")]
    [InlineData("a.tar.gz", "gz")]
    [InlineData("a", "")]
    [InlineData("a.", "")]
    [InlineData("", "")]
    public void GetFileExtension_StripsLeadingDot(string fileName, string expected)
    {
        var provider = new RecordingFileStorageProvider();

        Assert.Equal(expected, provider.CallGetFileExtension(fileName));
    }

    /// <summary>
    /// 哈希计算返回小写十六进制的 MD5
    /// </summary>
    /// <remarks>
    /// 用 RFC 1321 的标准测试向量而不是「再算一遍 MD5」，避免测试和实现一起错。
    /// </remarks>
    [Fact]
    public async Task ComputeFileHashAsync_ReturnsLowerCaseMd5Hex()
    {
        var provider = new RecordingFileStorageProvider();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));

        var hash = await provider.CallComputeFileHashAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", hash);
    }

    /// <summary>
    /// 空流的哈希是 MD5 的空输入定值
    /// </summary>
    [Fact]
    public async Task ComputeFileHashAsync_WithEmptyStream_ReturnsEmptyInputMd5()
    {
        var provider = new RecordingFileStorageProvider();
        using var stream = new MemoryStream();

        var hash = await provider.CallComputeFileHashAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", hash);
    }

    /// <summary>
    /// 批量上传按请求顺序逐个上传，结果与请求一一对应
    /// </summary>
    [Fact]
    public async Task UploadManyAsync_ReturnsResultsInRequestOrder()
    {
        var provider = new RecordingFileStorageProvider
        {
            UploadCoreHandler = request => new FileUploadResult { Success = true, Path = $"files/{request.FileName}" }
        };

        var results = await provider.UploadManyAsync(
            [new FileUploadRequest { FileName = "a.txt" }, new FileUploadRequest { FileName = "b.txt" }],
            TestContext.Current.CancellationToken);

        Assert.Equal(["a.txt", "b.txt"], provider.UploadedFileNames);
        Assert.Equal(["files/a.txt", "files/b.txt"], results.Select(result => result.Path));
        Assert.All(results, result => Assert.True(result.Success));
    }

    /// <summary>
    /// 单个文件上传失败只记入对应结果，其余文件照常上传
    /// </summary>
    [Fact]
    public async Task UploadManyAsync_WhenOneFails_KeepsUploadingOthers()
    {
        var provider = new RecordingFileStorageProvider
        {
            UploadCoreHandler = request => request.FileName == "bad.txt"
                ? throw new IOException("磁盘已满")
                : new FileUploadResult { Success = true, Path = request.FileName }
        };

        var results = await provider.UploadManyAsync(
            [new FileUploadRequest { FileName = "a.txt" }, new FileUploadRequest { FileName = "bad.txt" }, new FileUploadRequest { FileName = "c.txt" }],
            TestContext.Current.CancellationToken);

        Assert.Equal(["a.txt", "bad.txt", "c.txt"], provider.UploadedFileNames);
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.Equal("磁盘已满", results[1].ErrorMessage);
        Assert.True(results[2].Success);
    }

    /// <summary>
    /// 请求列表含空项时在上传任何文件之前拒绝
    /// </summary>
    [Fact]
    public async Task UploadManyAsync_WithNullItem_ThrowsBeforeUploading()
    {
        var provider = new RecordingFileStorageProvider();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.UploadManyAsync([new FileUploadRequest { FileName = "a.txt" }, null!], TestContext.Current.CancellationToken));

        Assert.Empty(provider.UploadedFileNames);
    }

    /// <summary>
    /// 已取消时不再上传并抛出取消异常，而不是把取消吞成失败结果
    /// </summary>
    [Fact]
    public async Task UploadManyAsync_WhenCanceled_Throws()
    {
        var provider = new RecordingFileStorageProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.UploadManyAsync([new FileUploadRequest { FileName = "a.txt" }], cancellation.Token));

        Assert.Empty(provider.UploadedFileNames);
    }

    /// <summary>
    /// 批量删除逐个删除去重后的路径
    /// </summary>
    [Fact]
    public async Task DeleteManyAsync_DeletesEachDistinctPath()
    {
        var provider = new RecordingFileStorageProvider();

        await provider.DeleteManyAsync(["a.txt", "b.txt", "a.txt"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["a.txt", "b.txt"], provider.DeletedPaths);
    }

    /// <summary>
    /// 部分路径删除失败时其余路径照常删除，结束后汇总抛出并指明失败路径
    /// </summary>
    [Fact]
    public async Task DeleteManyAsync_WhenSomeFail_DeletesOthersAndThrowsAggregate()
    {
        var provider = new RecordingFileStorageProvider();
        provider.FailingDeletePaths.Add("b.txt");

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => provider.DeleteManyAsync(["a.txt", "b.txt", "c.txt"], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(["a.txt", "c.txt"], provider.DeletedPaths);
        var failure = Assert.Single(exception.InnerExceptions);
        Assert.Contains("b.txt", failure.Message, StringComparison.Ordinal);
        Assert.IsType<IOException>(failure.InnerException);
    }

    /// <summary>
    /// 路径列表含空路径时在删除任何文件之前拒绝
    /// </summary>
    [Fact]
    public async Task DeleteManyAsync_WithBlankPath_ThrowsBeforeDeleting()
    {
        var provider = new RecordingFileStorageProvider();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.DeleteManyAsync(["a.txt", " "], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(provider.DeletedPaths);
    }

    /// <summary>
    /// 已取消时不再删除并抛出取消异常
    /// </summary>
    [Fact]
    public async Task DeleteManyAsync_WhenCanceled_Throws()
    {
        var provider = new RecordingFileStorageProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.DeleteManyAsync(["a.txt"], cancellationToken: cancellation.Token));

        Assert.Empty(provider.DeletedPaths);
    }
}

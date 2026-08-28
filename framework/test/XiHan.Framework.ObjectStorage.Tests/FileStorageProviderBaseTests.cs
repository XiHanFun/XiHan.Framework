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
}

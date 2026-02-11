#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileStorageProviderBase
// Guid:f6a7b8c9-d0e1-4c2f-a3b4-c5d6e7f8a9b0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using XiHan.Framework.ObjectStorage.Models;

namespace XiHan.Framework.ObjectStorage;

/// <summary>
/// 文件存储提供程序抽象基类
/// </summary>
public abstract class FileStorageProviderBase : IFileStorageProvider
{
    /// <summary>
    /// 存储类型名称
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// 是否支持分片上传
    /// </summary>
    public virtual bool SupportChunkedUpload => false;

    /// <summary>
    /// 是否支持断点续传
    /// </summary>
    public virtual bool SupportResumableUpload => false;

    /// <summary>
    /// 上传文件
    /// </summary>
    public virtual async Task<FileUploadResult> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await UploadCoreAsync(request, cancellationToken);
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// 初始化分片上传
    /// </summary>
    public virtual Task<string> InitiateChunkedUploadAsync(ChunkedUploadInitRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{ProviderName} does not support chunked upload");
    }

    /// <summary>
    /// 上传文件分片
    /// </summary>
    public virtual Task<ChunkUploadResult> UploadChunkAsync(ChunkUploadRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{ProviderName} does not support chunked upload");
    }

    /// <summary>
    /// 完成分片上传
    /// </summary>
    public virtual Task<FileUploadResult> CompleteChunkedUploadAsync(ChunkedUploadCompleteRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{ProviderName} does not support chunked upload");
    }

    /// <summary>
    /// 取消分片上传
    /// </summary>
    public virtual Task AbortChunkedUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{ProviderName} does not support chunked upload");
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    public abstract Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除文件
    /// </summary>
    public abstract Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public abstract Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    public abstract Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成预签名URL
    /// </summary>
    public virtual Task<string> GeneratePresignedUrlAsync(string path, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"{ProviderName} does not support presigned URLs");
    }

    /// <summary>
    /// 复制文件
    /// </summary>
    public abstract Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移动文件
    /// </summary>
    public abstract Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出目录下的文件
    /// </summary>
    public abstract Task<List<FileMetadata>> ListFilesAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 上传文件的核心实现
    /// </summary>
    protected abstract Task<FileUploadResult> UploadCoreAsync(FileUploadRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 规范化路径
    /// </summary>
    protected virtual string NormalizePath(string path)
    {
        return path.Replace("\\", "/").TrimStart('/');
    }

    /// <summary>
    /// 获取文件扩展名
    /// </summary>
    protected virtual string GetFileExtension(string fileName)
    {
        return Path.GetExtension(fileName).TrimStart('.');
    }

    /// <summary>
    /// 计算文件哈希
    /// </summary>
    protected virtual async Task<string> ComputeFileHashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 批量上传文件
    /// </summary>
    /// <remarks>
    /// 默认逐个调用 <see cref="UploadAsync"/>；支持原生批量接口的 Provider 可覆写以减少往返。
    /// </remarks>
    public virtual async Task<IReadOnlyList<FileUploadResult>> UploadManyAsync(IReadOnlyList<FileUploadRequest> requests, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Any(request => request is null))
        {
            throw new ArgumentException("上传请求列表不能包含空项。", nameof(requests));
        }

        var results = new List<FileUploadResult>(requests.Count);
        foreach (var request in requests)
        {
            // UploadAsync 会把异常收成失败结果，取消必须在它之外判定，否则会被吞成一条失败记录
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await UploadAsync(request, cancellationToken));
        }

        return results;
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
    /// 删除指定存储桶中的文件，默认忽略存储桶参数、转调默认存储桶的删除实现
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="bucketName">存储桶名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public virtual Task DeleteAsync(string path, string? bucketName, CancellationToken cancellationToken = default)
    {
        return DeleteAsync(path, cancellationToken);
    }

    /// <summary>
    /// 批量删除文件
    /// </summary>
    /// <remarks>
    /// 默认逐个调用 <see cref="DeleteAsync(string, string?, CancellationToken)"/>；支持原生批量删除的 Provider 可覆写以减少往返。
    /// </remarks>
    public virtual async Task DeleteManyAsync(IReadOnlyList<string> paths, string? bucketName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("文件路径列表不能包含空路径。", nameof(paths));
        }

        List<Exception>? failures = null;
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await DeleteAsync(path, bucketName, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                (failures ??= []).Add(new InvalidOperationException($"删除文件 '{path}' 失败：{ex.Message}", ex));
            }
        }

        if (failures is not null)
        {
            throw new AggregateException($"{failures.Count} 个文件删除失败。", failures);
        }
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public abstract Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查指定存储桶中的文件是否存在，默认忽略存储桶参数、转调默认存储桶的检查实现
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="bucketName">存储桶名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public virtual Task<bool> ExistsAsync(string path, string? bucketName, CancellationToken cancellationToken = default)
    {
        return ExistsAsync(path, cancellationToken);
    }

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    public abstract Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定存储桶中文件的元数据，默认忽略存储桶参数、转调默认存储桶的获取实现
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="bucketName">存储桶名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件元数据</returns>
    public virtual Task<FileMetadata> GetMetadataAsync(string path, string? bucketName, CancellationToken cancellationToken = default)
    {
        return GetMetadataAsync(path, cancellationToken);
    }

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
    /// 取对象键的最后一段作为文件名
    /// </summary>
    /// <remarks>
    /// 对象键是协议数据，分隔符只有 <c>/</c>；<c>\</c> 在 S3/OSS/COS 的键里是<b>合法字符</b>。
    /// 走 <see cref="Path.GetFileName(string)"/> 会按运行平台的分隔符语义拆——Windows 把 <c>\</c>
    /// 当分隔符、Linux 不当，同一个桶列出来的名字在两个平台就不一样。
    /// </remarks>
    /// <param name="objectKey">对象键</param>
    /// <returns>对象键的最后一段</returns>
    protected static string GetObjectName(string objectKey)
    {
        if (string.IsNullOrEmpty(objectKey))
        {
            return string.Empty;
        }

        var separatorIndex = objectKey.LastIndexOf('/');
        return separatorIndex < 0 ? objectKey : objectKey[(separatorIndex + 1)..];
    }

    /// <summary>
    /// 获取文件扩展名
    /// </summary>
    /// <remarks>
    /// 同 <see cref="GetObjectName(string)"/>：只认 <c>/</c>，不看运行平台。
    /// </remarks>
    protected virtual string GetFileExtension(string fileName)
    {
        var name = GetObjectName(fileName);
        var extensionIndex = name.LastIndexOf('.');
        return extensionIndex > 0 ? name[(extensionIndex + 1)..] : string.Empty;
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

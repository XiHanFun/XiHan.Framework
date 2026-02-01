#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalFileStorageProvider
// Guid:a7b8c9d0-e1f2-4d3a-b4c5-d6e7f8a9b0c1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Models;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Providers;

/// <summary>
/// 本地文件存储提供程序
/// </summary>
public class LocalFileStorageProvider : FileStorageProviderBase
{
    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, ChunkedUploadSession> _uploadSessions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">本地存储配置</param>
    public LocalFileStorageProvider(IOptions<LocalStorageOptions> options)
    {
        var storageOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
        _rootPath = Path.GetFullPath(storageOptions.RootPath);
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
        }
    }

    /// <summary>
    /// 存储类型名称
    /// </summary>
    public override string ProviderName => "Local";

    /// <summary>
    /// 是否支持分片上传
    /// </summary>
    public override bool SupportChunkedUpload => true;

    /// <summary>
    /// 是否支持断点续传
    /// </summary>
    public override bool SupportResumableUpload => true;

    /// <summary>
    /// 初始化分片上传
    /// </summary>
    public override Task<string> InitiateChunkedUploadAsync(ChunkedUploadInitRequest request, CancellationToken cancellationToken = default)
    {
        var uploadId = Guid.NewGuid().ToString("N");
        var tempDir = Path.Combine(Path.GetTempPath(), "chunked-uploads", uploadId);
        Directory.CreateDirectory(tempDir);

        var session = new ChunkedUploadSession
        {
            UploadId = uploadId,
            FileName = request.FileName,
            StoragePath = request.StoragePath,
            TotalSize = request.TotalSize,
            ChunkSize = request.ChunkSize,
            TempDirectory = tempDir,
            UploadedChunks = new ConcurrentDictionary<int, string>(),
            CreatedTime = DateTimeOffset.Now
        };

        _uploadSessions[uploadId] = session;

        return Task.FromResult(uploadId);
    }

    /// <summary>
    /// 上传文件分片
    /// </summary>
    public override async Task<ChunkUploadResult> UploadChunkAsync(ChunkUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (!_uploadSessions.TryGetValue(request.UploadId, out var session))
        {
            return new ChunkUploadResult
            {
                Success = false,
                ChunkNumber = request.ChunkNumber,
                ErrorMessage = "Upload session not found"
            };
        }

        try
        {
            var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{request.ChunkNumber:D4}");

            using var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await request.ChunkData.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            // 计算分片哈希
            string etag;
            using (var readStream = File.OpenRead(chunkPath))
            {
                etag = await ComputeFileHashAsync(readStream, cancellationToken);
            }

            session.UploadedChunks[request.ChunkNumber] = etag;

            return new ChunkUploadResult
            {
                Success = true,
                ChunkNumber = request.ChunkNumber,
                ETag = etag
            };
        }
        catch (Exception ex)
        {
            return new ChunkUploadResult
            {
                Success = false,
                ChunkNumber = request.ChunkNumber,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 完成分片上传
    /// </summary>
    public override async Task<FileUploadResult> CompleteChunkedUploadAsync(ChunkedUploadCompleteRequest request, CancellationToken cancellationToken = default)
    {
        if (!_uploadSessions.TryGetValue(request.UploadId, out var session))
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = "Upload session not found"
            };
        }

        try
        {
            var fullPath = GetFullPath(request.StoragePath);
            var directory = Path.GetDirectoryName(fullPath)!;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 合并所有分片
            using var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

            foreach (var chunkInfo in request.ChunkInfos.OrderBy(c => c.ChunkNumber))
            {
                var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{chunkInfo.ChunkNumber:D4}");
                if (!File.Exists(chunkPath))
                {
                    return new FileUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"Chunk {chunkInfo.ChunkNumber} not found"
                    };
                }

                using var chunkStream = File.OpenRead(chunkPath);
                await chunkStream.CopyToAsync(outputStream, cancellationToken);
            }

            await outputStream.FlushAsync(cancellationToken);

            var fileInfo = new FileInfo(fullPath);

            // 计算文件哈希
            string etag;
            using (var readStream = File.OpenRead(fullPath))
            {
                etag = await ComputeFileHashAsync(readStream, cancellationToken);
            }

            // 清理临时文件
            CleanupChunkedUpload(request.UploadId);

            return new FileUploadResult
            {
                Success = true,
                Path = request.StoragePath,
                FullPath = fullPath,
                Url = GetFileUrl(request.StoragePath),
                FileSize = fileInfo.Length,
                ETag = etag
            };
        }
        catch (Exception ex)
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 取消分片上传
    /// </summary>
    public override Task AbortChunkedUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        CleanupChunkedUpload(uploadId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    public override Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    public override Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public override Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(File.Exists(fullPath));
    }

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    public override Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var fileInfo = new FileInfo(fullPath);

        return Task.FromResult(new FileMetadata
        {
            Name = fileInfo.Name,
            Path = path,
            Size = fileInfo.Length,
            ContentType = GetContentType(fileInfo.Extension),
            LastModified = fileInfo.LastWriteTimeUtc,
            IsDirectory = false,
            Url = GetFileUrl(path)
        });
    }

    /// <summary>
    /// 复制文件
    /// </summary>
    public override Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var sourceFullPath = GetFullPath(sourcePath);
        var destFullPath = GetFullPath(destinationPath);

        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        var directory = Path.GetDirectoryName(destFullPath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourceFullPath, destFullPath, true);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 移动文件
    /// </summary>
    public override Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var sourceFullPath = GetFullPath(sourcePath);
        var destFullPath = GetFullPath(destinationPath);

        if (!File.Exists(sourceFullPath))
        {
            throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        var directory = Path.GetDirectoryName(destFullPath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Move(sourceFullPath, destFullPath, true);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 列出目录下的文件
    /// </summary>
    public override Task<List<FileMetadata>> ListFilesAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(new List<FileMetadata>());
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(fullPath, "*.*", searchOption);

        var metadata = files.Select(file =>
        {
            var fileInfo = new FileInfo(file);
            var relativePath = Path.GetRelativePath(_rootPath, file).Replace("\\", "/");

            return new FileMetadata
            {
                Name = fileInfo.Name,
                Path = relativePath,
                Size = fileInfo.Length,
                ContentType = GetContentType(fileInfo.Extension),
                LastModified = fileInfo.LastWriteTimeUtc,
                IsDirectory = false,
                Url = GetFileUrl(relativePath)
            };
        }).ToList();

        return Task.FromResult(metadata);
    }

    /// <summary>
    /// 上传文件的核心实现
    /// </summary>
    protected override async Task<FileUploadResult> UploadCoreAsync(FileUploadRequest request, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(request.StoragePath);
        var directory = Path.GetDirectoryName(fullPath)!;

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!request.Overwrite && File.Exists(fullPath))
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = "File already exists"
            };
        }

        using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920]; // 80KB buffer
        long totalBytes = 0;
        int bytesRead;

        while ((bytesRead = await request.FileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytes += bytesRead;
            request.ProgressCallback?.Invoke(totalBytes, request.FileStream.Length);
        }

        await fileStream.FlushAsync(cancellationToken);

        var fileInfo = new FileInfo(fullPath);

        return new FileUploadResult
        {
            Success = true,
            Path = request.StoragePath,
            FullPath = fullPath,
            Url = GetFileUrl(request.StoragePath),
            FileSize = fileInfo.Length,
            ETag = await ComputeFileHashAsync(File.OpenRead(fullPath), cancellationToken)
        };
    }

    #region 私有方法

    /// <summary>
    /// 获取完整路径
    /// </summary>
    private string GetFullPath(string relativePath)
    {
        var normalizedPath = NormalizePath(relativePath);
        return Path.GetFullPath(Path.Combine(_rootPath, normalizedPath));
    }

    /// <summary>
    /// 获取文件URL
    /// </summary>
    private string GetFileUrl(string relativePath)
    {
        // 对于本地存储，返回相对路径
        // 实际使用时需要配合Web服务器的静态文件服务
        return "/" + NormalizePath(relativePath);
    }

    /// <summary>
    /// 获取内容类型
    /// </summary>
    private string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// 清理分片上传临时文件
    /// </summary>
    private void CleanupChunkedUpload(string uploadId)
    {
        if (_uploadSessions.TryRemove(uploadId, out var session))
        {
            try
            {
                if (Directory.Exists(session.TempDirectory))
                {
                    Directory.Delete(session.TempDirectory, true);
                }
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    #endregion

    /// <summary>
    /// 分片上传会话
    /// </summary>
    private class ChunkedUploadSession
    {
        public string UploadId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public int ChunkSize { get; set; }
        public string TempDirectory { get; set; } = string.Empty;
        public ConcurrentDictionary<int, string> UploadedChunks { get; set; } = new();
        public DateTimeOffset CreatedTime { get; set; }
    }
}

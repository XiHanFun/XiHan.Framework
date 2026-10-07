// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using XiHan.Framework.ObjectStorage.Models;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Providers;

/// <summary>
/// 本地文件存储提供程序
/// </summary>
public class LocalFileStorageProvider : FileStorageProviderBase
{
    private static readonly TimeSpan UploadSessionTtl = TimeSpan.FromHours(24);

    private readonly string _rootPath;
    private readonly string _urlPrefix;
    private readonly ConcurrentDictionary<string, ChunkedUploadSession> _uploadSessions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">本地存储配置</param>
    public LocalFileStorageProvider(IOptions<LocalStorageOptions> options)
    {
        var storageOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
        _rootPath = Path.GetFullPath(storageOptions.RootPath);
        _urlPrefix = NormalizeUrlPrefix(storageOptions.UrlPrefix);
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
        CleanupExpiredChunkedUploads();

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
            LastActivityUtcTicks = DateTimeOffset.UtcNow.UtcTicks
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

        var gateAcquired = false;
        try
        {
            await session.Gate.WaitAsync(cancellationToken);
            gateAcquired = true;

            if (!IsCurrentChunkedUploadSession(request.UploadId, session))
            {
                return new ChunkUploadResult
                {
                    Success = false,
                    ChunkNumber = request.ChunkNumber,
                    ErrorMessage = "Upload session not found"
                };
            }

            Interlocked.Exchange(ref session.LastActivityUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
            var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{request.ChunkNumber:D4}");

            // 写入流必须在算哈希之前就释放：它是 FileShare.None 独占持有的，
            // 早先这里写成 using var（作用域到方法结束），下面的 File.OpenRead 读的正是同一个文件，
            // 必然抛「文件正被另一进程使用」，再被下面的 catch 吞成 Success=false ——
            // 结果是本地存储的分片上传每一次都失败，功能实际不可用。
            await using (var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await request.ChunkData.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

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
        finally
        {
            if (gateAcquired)
            {
                session.Gate.Release();
            }
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

        var gateAcquired = false;
        string? temporaryPath = null;
        try
        {
            await session.Gate.WaitAsync(cancellationToken);
            gateAcquired = true;

            if (!IsCurrentChunkedUploadSession(request.UploadId, session))
            {
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = "Upload session not found"
                };
            }

            Interlocked.Exchange(ref session.LastActivityUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
            var fullPath = GetFullPath(request.StoragePath);
            var directory = Path.GetDirectoryName(fullPath)!;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.uploading");
            await using (var outputStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
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
            }

            string etag;
            using (var readStream = File.OpenRead(temporaryPath))
            {
                etag = await ComputeFileHashAsync(readStream, cancellationToken);
            }

            File.Move(temporaryPath, fullPath, true);
            temporaryPath = null;
            var fileInfo = new FileInfo(fullPath);
            CleanupChunkedUpload(session);

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
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // 临时输出清理失败不覆盖原始合并错误。
                }
            }

            if (gateAcquired)
            {
                session.Gate.Release();
            }
        }
    }

    /// <summary>
    /// 取消分片上传
    /// </summary>
    public override async Task AbortChunkedUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        if (!_uploadSessions.TryGetValue(uploadId, out var session))
        {
            return;
        }

        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            if (IsCurrentChunkedUploadSession(uploadId, session))
            {
                CleanupChunkedUpload(session);
            }
        }
        finally
        {
            session.Gate.Release();
        }
    }

    /// <summary>
    /// 生成预签名URL
    /// </summary>
    /// <remarks>
    /// 本地存储无对象存储的签名机制，文件经 Web 静态文件服务（UseStaticFiles 暴露 UrlPrefix 目录）公开访问，
    /// 故直接返回 <see cref="GetFileUrl"/> 的静态可访问 URL；<paramref name="expiresIn"/> 对本地存储无意义，忽略之。
    /// 调用方若需要鉴权或时效控制，应改用支持真实预签名的云存储 Provider。
    /// </remarks>
    public override Task<string> GeneratePresignedUrlAsync(string path, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetFileUrl(path));
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

        long totalBytes = 0;
        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920]; // 80KB buffer
            int bytesRead;

            while ((bytesRead = await request.FileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytes += bytesRead;
                request.ProgressCallback?.Invoke(totalBytes, request.FileStream.Length);
            }

            await fileStream.FlushAsync(cancellationToken);
        }

        var fileInfo = new FileInfo(fullPath);
        await using var readStream = File.OpenRead(fullPath);

        return new FileUploadResult
        {
            Success = true,
            Path = request.StoragePath,
            FullPath = fullPath,
            Url = GetFileUrl(request.StoragePath),
            FileSize = fileInfo.Length,
            ETag = await ComputeFileHashAsync(readStream, cancellationToken)
        };
    }

    #region 私有方法

    /// <summary>
    /// 获取完整路径
    /// </summary>
    private string GetFullPath(string relativePath)
    {
        var normalizedPath = NormalizeLocalPath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedPath));
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPathPrefix = Path.EndsInDirectorySeparator(_rootPath)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.Equals(_rootPath, pathComparison) && !fullPath.StartsWith(rootPathPrefix, pathComparison))
        {
            throw new UnauthorizedAccessException("Storage path must remain within the configured root directory.");
        }

        EnsureNoReparsePoints(fullPath);
        return fullPath;
    }

    /// <summary>
    /// 拒绝通过根目录内已有的重解析点访问其他位置
    /// </summary>
    private void EnsureNoReparsePoints(string fullPath)
    {
        var relativePath = Path.GetRelativePath(_rootPath, fullPath);
        if (relativePath == ".")
        {
            return;
        }

        var currentPath = _rootPath;
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            currentPath = Path.Combine(currentPath, segment);

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(currentPath);
            }
            catch (FileNotFoundException)
            {
                break;
            }
            catch (DirectoryNotFoundException)
            {
                break;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException("Storage paths cannot traverse symbolic links or reparse points.");
            }
        }
    }

    /// <summary>
    /// 获取文件URL
    /// </summary>
    private string GetFileUrl(string relativePath)
    {
        // 对于本地存储，返回 URL 前缀下的相对路径
        // 实际使用时需要配合Web服务器的静态文件服务
        var normalizedPath = NormalizeLocalPath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return _urlPrefix;
        }

        return _urlPrefix == "/"
            ? $"/{normalizedPath}"
            : $"{_urlPrefix}/{normalizedPath}";
    }

    /// <summary>
    /// 规范化本地存储相对路径
    /// </summary>
    private string NormalizeLocalPath(string relativePath)
    {
        var normalizedPath = NormalizePath(relativePath);
        var urlPrefixSegment = NormalizePath(_urlPrefix);

        if (string.IsNullOrWhiteSpace(urlPrefixSegment))
        {
            return normalizedPath;
        }

        if (normalizedPath.Equals(urlPrefixSegment, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return normalizedPath.StartsWith($"{urlPrefixSegment}/", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath[(urlPrefixSegment.Length + 1)..]
            : normalizedPath;
    }

    /// <summary>
    /// 规范化 URL 前缀
    /// </summary>
    private static string NormalizeUrlPrefix(string? urlPrefix)
    {
        if (string.IsNullOrWhiteSpace(urlPrefix))
        {
            return "/";
        }

        var normalized = "/" + urlPrefix.Trim().Trim('/');
        return normalized == "/" ? "/" : normalized;
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
    /// 判断分片上传会话是否仍在会话表中
    /// </summary>
    private bool IsCurrentChunkedUploadSession(string uploadId, ChunkedUploadSession session)
    {
        return _uploadSessions.TryGetValue(uploadId, out var currentSession)
            && ReferenceEquals(session, currentSession);
    }

    /// <summary>
    /// 清理分片上传临时文件
    /// </summary>
    private void CleanupChunkedUpload(ChunkedUploadSession session)
    {
        var entry = new KeyValuePair<string, ChunkedUploadSession>(session.UploadId, session);
        if (((ICollection<KeyValuePair<string, ChunkedUploadSession>>)_uploadSessions).Remove(entry))
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

    private void CleanupExpiredChunkedUploads()
    {
        var cutoffTicks = DateTimeOffset.UtcNow.Subtract(UploadSessionTtl).UtcTicks;
        foreach (var session in _uploadSessions)
        {
            if (!session.Value.Gate.Wait(0))
            {
                continue;
            }

            try
            {
                if (IsCurrentChunkedUploadSession(session.Key, session.Value)
                    && Interlocked.Read(ref session.Value.LastActivityUtcTicks) <= cutoffTicks)
                {
                    CleanupChunkedUpload(session.Value);
                }
            }
            finally
            {
                session.Value.Gate.Release();
            }
        }
    }

    #endregion

    /// <summary>
    /// 分片上传会话
    /// </summary>
    private class ChunkedUploadSession
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public string UploadId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public int ChunkSize { get; set; }
        public string TempDirectory { get; set; } = string.Empty;
        public ConcurrentDictionary<int, string> UploadedChunks { get; set; } = new();
        public long LastActivityUtcTicks;
    }
}

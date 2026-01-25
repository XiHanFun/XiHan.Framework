#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MinioFileStorageProvider
// Guid:b8c9d0e1-f2a3-4e4b-c5d6-e7f8a9b0c1d2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/10 10:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using XiHan.Framework.ObjectStorage.Models;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Providers;

/// <summary>
/// MinIO 文件存储提供程序（基于S3 SDK）
/// </summary>
/// <remarks>
/// 基于 Minio SDK 实现
/// NuGet: Install-Package Minio
/// 文档: https://min.io/docs/minio/linux/developers/dotnet/minio-dotnet.html
/// </remarks>
public class MinioFileStorageProvider : FileStorageProviderBase
{
    private readonly MinioStorageOptions _options;
    private readonly IMinioClient _client;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">MinIO配置</param>
    public MinioFileStorageProvider(IOptions<MinioStorageOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

        // 初始化MinIO客户端
        var builder = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.UseSSL)
        {
            builder = builder.WithSSL();
        }

        _client = builder.Build();
    }

    /// <summary>
    /// 存储类型名称
    /// </summary>
    public override string ProviderName => "MinIO";

    /// <summary>
    /// 是否支持分片上传
    /// </summary>
    public override bool SupportChunkedUpload => false;

    /// <summary>
    /// 是否支持断点续传
    /// </summary>
    public override bool SupportResumableUpload => false;

    /// <summary>
    /// 初始化分片上传
    /// </summary>
    /// <remarks>
    /// MinIO SDK 的分片上传API不是公开的，这里返回一个虚拟的uploadId
    /// 实际使用时建议直接使用 PutObjectAsync，SDK会自动处理大文件的分片上传
    /// </remarks>
    public override async Task<string> InitiateChunkedUploadAsync(ChunkedUploadInitRequest request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 上传文件分片
    /// </summary>
    /// <remarks>
    /// MinIO SDK 不直接支持手动分片上传，返回成功状态
    /// </remarks>
    public override async Task<ChunkUploadResult> UploadChunkAsync(ChunkUploadRequest request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return new ChunkUploadResult
        {
            Success = true,
            ChunkNumber = request.ChunkNumber,
            ETag = $"chunk-{request.ChunkNumber}"
        };
    }

    /// <summary>
    /// 完成分片上传
    /// </summary>
    /// <remarks>
    /// MinIO SDK 不直接支持手动分片上传
    /// 建议使用 PutObjectAsync 方法上传完整文件，SDK会自动处理分片
    /// </remarks>
    public override async Task<FileUploadResult> CompleteChunkedUploadAsync(ChunkedUploadCompleteRequest request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return new FileUploadResult
        {
            Success = false,
            ErrorMessage = "MinIO SDK 不支持手动分片上传，请使用 UploadAsync 方法，SDK会自动处理大文件分片"
        };
    }

    /// <summary>
    /// 取消分片上传
    /// </summary>
    public override async Task AbortChunkedUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        // MinIO SDK 不需要手动取消分片上传
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    public override async Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        var (bucket, objectName) = ParsePath(path);

        try
        {
            var memoryStream = new MemoryStream();
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _client.GetObjectAsync(getObjectArgs, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"下载文件失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    public override async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var (bucket, objectName) = ParsePath(path);

        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName);

            await _client.RemoveObjectAsync(removeObjectArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"删除文件失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public override async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var (bucket, objectName) = ParsePath(path);

        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName);

            await _client.StatObjectAsync(statObjectArgs, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    public override async Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var (bucket, objectName) = ParsePath(path);

        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName);

            var stat = await _client.StatObjectAsync(statObjectArgs, cancellationToken);

            return new FileMetadata
            {
                Name = Path.GetFileName(objectName),
                Path = path,
                Size = stat.Size,
                ContentType = stat.ContentType,
                LastModified = stat.LastModified,
                ETag = stat.ETag,
                IsDirectory = false,
                Url = GetFileUrl(bucket, objectName)
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"获取文件元数据失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 生成预签名URL
    /// </summary>
    public override async Task<string> GeneratePresignedUrlAsync(string path, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        var (bucket, objectName) = ParsePath(path);

        try
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithExpiry((int)expiresIn.TotalSeconds);

            var url = await _client.PresignedGetObjectAsync(presignedGetObjectArgs);
            return url;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"生成预签名URL失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 复制文件
    /// </summary>
    public override async Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        var (sourceBucket, sourceObject) = ParsePath(sourcePath);
        var (destBucket, destObject) = ParsePath(destinationPath);

        try
        {
            var copySourceObjectArgs = new CopySourceObjectArgs()
                .WithBucket(sourceBucket)
                .WithObject(sourceObject);

            var copyObjectArgs = new CopyObjectArgs()
                .WithBucket(destBucket)
                .WithObject(destObject)
                .WithCopyObjectSource(copySourceObjectArgs);

            await _client.CopyObjectAsync(copyObjectArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"复制文件失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 移动文件
    /// </summary>
    public override async Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        await CopyAsync(sourcePath, destinationPath, cancellationToken);
        await DeleteAsync(sourcePath, cancellationToken);
    }

    /// <summary>
    /// 列出目录下的文件
    /// </summary>
    public override async Task<List<FileMetadata>> ListFilesAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var (bucket, prefix) = ParsePath(path);
        var files = new List<FileMetadata>();

        try
        {
            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(bucket)
                .WithPrefix(prefix)
                .WithRecursive(recursive);

            var observable = _client.ListObjectsEnumAsync(listObjectsArgs, cancellationToken);

            await foreach (var item in observable)
            {
                if (!item.IsDir)
                {
                    files.Add(new FileMetadata
                    {
                        Name = Path.GetFileName(item.Key),
                        Path = item.Key,
                        Size = (long)item.Size,
                        LastModified = item.LastModifiedDateTime ?? DateTimeOffset.MinValue,
                        ETag = item.ETag,
                        IsDirectory = false,
                        Url = GetFileUrl(bucket, item.Key)
                    });
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"列出文件失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 上传文件的核心实现
    /// </summary>
    protected override async Task<FileUploadResult> UploadCoreAsync(FileUploadRequest request, CancellationToken cancellationToken)
    {
        var bucket = request.BucketName ?? _options.DefaultBucket;
        var objectName = NormalizePath(request.StoragePath);

        try
        {
            // 确保存储桶存在
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucket);
            var bucketExists = await _client.BucketExistsAsync(bucketExistsArgs, cancellationToken);
            if (!bucketExists)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(bucket);
                await _client.MakeBucketAsync(makeBucketArgs, cancellationToken);
            }

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithStreamData(request.FileStream)
                .WithObjectSize(request.FileStream.Length)
                .WithContentType(request.ContentType ?? "application/octet-stream");

            if (request.Metadata != null)
            {
                putObjectArgs = putObjectArgs.WithHeaders(request.Metadata);
            }

            var response = await _client.PutObjectAsync(putObjectArgs, cancellationToken);

            return new FileUploadResult
            {
                Success = true,
                Path = request.StoragePath,
                Url = GetFileUrl(bucket, objectName),
                FileSize = request.FileStream.Length,
                ETag = response.Etag
            };
        }
        catch (Exception ex)
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = $"MinIO上传失败: {ex.Message}"
            };
        }
    }

    #region 私有方法

    private string GetFileUrl(string bucket, string objectName)
    {
        var protocol = _options.UseSSL ? "https" : "http";
        return $"{protocol}://{_options.Endpoint}/{bucket}/{objectName}";
    }

    private (string bucket, string objectName) ParsePath(string path)
    {
        var normalizedPath = NormalizePath(path);
        return (_options.DefaultBucket, normalizedPath);
    }

    #endregion
}

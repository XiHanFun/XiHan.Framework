#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AliyunOssStorageProvider
// Guid:a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/10 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Aliyun.OSS;
using Aliyun.OSS.Common;
using XiHan.Framework.VirtualFileSystem.Storage.Models;
using XiHan.Framework.VirtualFileSystem.Storage.Options;

namespace XiHan.Framework.VirtualFileSystem.Storage.Providers;

/// <summary>
/// 阿里云 OSS 文件存储提供程序
/// </summary>
/// <remarks>
/// 基于 Aliyun.OSS.SDK 实现
/// NuGet: Install-Package Aliyun.OSS.SDK.NetCore
/// 文档: https://help.aliyun.com/zh/oss/developer-reference/net
/// </remarks>
public class AliyunOssStorageProvider : FileStorageProviderBase
{
    private readonly AliyunOssStorageOptions _options;
    private readonly OssClient _client;
    private readonly ConcurrentDictionary<string, MultipartUploadSession> _uploadSessions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public AliyunOssStorageProvider(AliyunOssStorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        var endpoint = _options.UseInternal
            ? _options.Endpoint.Replace("oss-", "oss-internal-")
            : _options.Endpoint;
        _client = new OssClient(endpoint, _options.AccessKeyId, _options.AccessKeySecret);
    }

    /// <summary>
    /// 存储类型名称
    /// </summary>
    public override string ProviderName => "AliyunOSS";

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
    public override async Task<string> InitiateChunkedUploadAsync(ChunkedUploadInitRequest request, CancellationToken cancellationToken = default)
    {
        var bucket = request.BucketName ?? _options.DefaultBucket;
        var objectName = NormalizePath(request.StoragePath);

        try
        {
            var initRequest = new InitiateMultipartUploadRequest(bucket, objectName);

            if (request.ContentType != null)
            {
                var metadata = new ObjectMetadata
                {
                    ContentType = request.ContentType
                };
                initRequest.ObjectMetadata = metadata;
            }

            var result = _client.InitiateMultipartUpload(initRequest);
            var uploadId = result.UploadId;

            _uploadSessions[uploadId] = new MultipartUploadSession
            {
                UploadId = uploadId,
                BucketName = bucket,
                ObjectName = objectName,
                PartETags = new ConcurrentDictionary<int, string>()
            };

            return uploadId;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"初始化分片上传失败: {ex.Message}", ex);
        }
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
                ErrorMessage = "上传会话不存在"
            };
        }

        try
        {
            var uploadRequest = new UploadPartRequest(
                session.BucketName,
                session.ObjectName,
                request.UploadId)
            {
                InputStream = request.ChunkData,
                PartSize = request.ChunkSize,
                PartNumber = request.ChunkNumber
            };

            var result = _client.UploadPart(uploadRequest);
            var etag = result.ETag;

            session.PartETags[request.ChunkNumber] = etag;

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
                ErrorMessage = "上传会话不存在"
            };
        }

        try
        {
            var partETags = request.ChunkInfos
                .OrderBy(c => c.ChunkNumber)
                .Select(c => new PartETag(c.ChunkNumber, c.ETag))
                .ToList();

            var completeRequest = new CompleteMultipartUploadRequest(
                session.BucketName,
                session.ObjectName,
                request.UploadId);

            // 添加所有分片的ETag
            foreach (var partETag in partETags)
            {
                completeRequest.PartETags.Add(partETag);
            }

            var result = _client.CompleteMultipartUpload(completeRequest);

            _uploadSessions.TryRemove(request.UploadId, out _);

            return new FileUploadResult
            {
                Success = true,
                Path = request.StoragePath,
                Url = GetObjectUrl(session.BucketName, session.ObjectName),
                ETag = result.ETag
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
    public override async Task AbortChunkedUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        if (_uploadSessions.TryRemove(uploadId, out var session))
        {
            try
            {
                var abortRequest = new AbortMultipartUploadRequest(
                    session.BucketName,
                    session.ObjectName,
                    uploadId);
                _client.AbortMultipartUpload(abortRequest);
            }
            catch (OssException)
            {
                // 忽略错误，会话已经被移除
            }
        }
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    public override async Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        var (bucket, objectName) = ParsePath(path);

        try
        {
            var obj = _client.GetObject(bucket, objectName);
            var memoryStream = new MemoryStream();
            using (obj.Content)
            {
                await obj.Content.CopyToAsync(memoryStream, cancellationToken);
            }
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
            _client.DeleteObject(bucket, objectName);
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
            return _client.DoesObjectExist(bucket, objectName);
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
            var metadata = _client.GetObjectMetadata(bucket, objectName);

            return new FileMetadata
            {
                Name = Path.GetFileName(objectName),
                Path = path,
                Size = metadata.ContentLength,
                ContentType = metadata.ContentType,
                LastModified = metadata.LastModified,
                ETag = metadata.ETag,
                IsDirectory = false,
                Url = GetObjectUrl(bucket, objectName)
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
            var req = new GeneratePresignedUriRequest(bucket, objectName, SignHttpMethod.Get)
            {
                Expiration = DateTime.Now.Add(expiresIn)
            };
            var uri = _client.GeneratePresignedUri(req);
            return uri.ToString();
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
            var request = new CopyObjectRequest(sourceBucket, sourceObject, destBucket, destObject);
            _client.CopyObject(request);
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
            var listRequest = new ListObjectsRequest(bucket)
            {
                Prefix = prefix,
                Delimiter = recursive ? null : "/"
            };

            ObjectListing result;
            do
            {
                result = _client.ListObjects(listRequest);

                foreach (var obj in result.ObjectSummaries)
                {
                    files.Add(new FileMetadata
                    {
                        Name = Path.GetFileName(obj.Key),
                        Path = obj.Key,
                        Size = obj.Size,
                        LastModified = obj.LastModified,
                        ETag = obj.ETag,
                        IsDirectory = false,
                        Url = GetObjectUrl(bucket, obj.Key)
                    });
                }

                listRequest.Marker = result.NextMarker;
            } while (result.IsTruncated);

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
            var metadata = new ObjectMetadata();
            if (request.ContentType != null)
            {
                metadata.ContentType = request.ContentType;
            }
            if (request.CacheControl != null)
            {
                metadata.CacheControl = request.CacheControl;
            }
            if (request.Metadata != null)
            {
                foreach (var kvp in request.Metadata)
                {
                    metadata.UserMetadata.Add(kvp.Key, kvp.Value);
                }
            }

            var putRequest = new PutObjectRequest(bucket, objectName, request.FileStream, metadata);

            var result = _client.PutObject(putRequest);

            // 设置访问控制（如果需要）
            if (request.AccessControl != null)
            {
                var cannedAcl = request.AccessControl switch
                {
                    "public-read" => CannedAccessControlList.PublicRead,
                    "private" => CannedAccessControlList.Private,
                    _ => CannedAccessControlList.Private
                };
                var setAclRequest = new SetObjectAclRequest(bucket, objectName, cannedAcl);
                _client.SetObjectAcl(setAclRequest);
            }

            return new FileUploadResult
            {
                Success = true,
                Path = request.StoragePath,
                Url = GetObjectUrl(bucket, objectName),
                FileSize = request.FileStream.Length,
                ETag = result.ETag
            };
        }
        catch (Exception ex)
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = $"阿里云OSS上传失败: {ex.Message}"
            };
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取对象URL
    /// </summary>
    private string GetObjectUrl(string bucket, string objectName)
    {
        if (!string.IsNullOrEmpty(_options.CdnDomain))
        {
            return $"https://{_options.CdnDomain}/{objectName}";
        }

        return $"https://{bucket}.{_options.Endpoint}/{objectName}";
    }

    /// <summary>
    /// 解析路径
    /// </summary>
    private (string bucket, string objectName) ParsePath(string path)
    {
        var normalizedPath = NormalizePath(path);
        return (_options.DefaultBucket, normalizedPath);
    }

    #endregion

    /// <summary>
    /// 分片上传会话
    /// </summary>
    private class MultipartUploadSession
    {
        public string UploadId { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public ConcurrentDictionary<int, string> PartETags { get; set; } = new();
    }
}

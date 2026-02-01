#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TencentCosStorageProvider
// Guid:b2c3d4e5-f6a7-48b9-c0d1-e2f3a4b5c6d7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 11:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Bucket;
using COSXML.Model.Object;
using COSXML.Model.Tag;
using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Models;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Providers;

/// <summary>
/// 腾讯云 COS 文件存储提供程序
/// </summary>
/// <remarks>
/// 基于 COSXML SDK 实现
/// NuGet: Install-Package Tencent.QCloud.Cos.Sdk
/// 文档: https://cloud.tencent.com/document/product/436/32819
/// </remarks>
public class TencentCosStorageProvider : FileStorageProviderBase
{
    private readonly TencentCosStorageOptions _options;
    private readonly CosXml _cosXml;
    private readonly ConcurrentDictionary<string, MultipartUploadSession> _uploadSessions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">腾讯云COS配置</param>
    public TencentCosStorageProvider(IOptions<TencentCosStorageOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

        var config = new CosXmlConfig.Builder()
            .IsHttps(true)
            .SetRegion(_options.Region)
            .SetDebugLog(false)
            .Build();

        var credential = new DefaultQCloudCredentialProvider(
            _options.SecretId,
            _options.SecretKey,
            3600);

        _cosXml = new CosXmlServer(config, credential);
    }

    /// <summary>
    /// 存储类型名称
    /// </summary>
    public override string ProviderName => "TencentCOS";

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
        var bucket = request.BucketName ?? GetFullBucketName(_options.DefaultBucket);
        var objectKey = NormalizePath(request.StoragePath);

        try
        {
            var initRequest = new InitMultipartUploadRequest(bucket, objectKey);

            if (request.ContentType != null)
            {
                initRequest.SetRequestHeader("Content-Type", request.ContentType);
            }

            var result = _cosXml.InitMultipartUpload(initRequest);
            var uploadId = result.initMultipartUpload.uploadId;

            _uploadSessions[uploadId] = new MultipartUploadSession
            {
                UploadId = uploadId,
                BucketName = bucket,
                ObjectKey = objectKey,
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

        var bucket = request.BucketName ?? _options.DefaultBucket;
        var key = NormalizePath(request.StoragePath);

        try
        {
            var putRequest = new PutObjectRequest(bucket, key, request.StoragePath);

            var result = _cosXml.PutObject(putRequest);

            return new ChunkUploadResult
            {
                Success = true,
                ChunkNumber = request.ChunkNumber,
                ETag = result.eTag
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
            var completeRequest = new CompleteMultipartUploadRequest(
                session.BucketName,
                session.ObjectKey,
                request.UploadId);

            var partNumbersAndETags = new Dictionary<int, string>();
            foreach (var chunk in request.ChunkInfos.OrderBy(c => c.ChunkNumber))
            {
                partNumbersAndETags[chunk.ChunkNumber] = chunk.ETag!;
            }
            completeRequest.SetPartNumberAndETag(partNumbersAndETags);

            var result = _cosXml.CompleteMultiUpload(completeRequest);

            _uploadSessions.TryRemove(request.UploadId, out _);

            return new FileUploadResult
            {
                Success = true,
                Path = request.StoragePath,
                Url = GetObjectUrl(session.BucketName, session.ObjectKey),
                ETag = result.completeResult.eTag
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
                    session.ObjectKey,
                    uploadId);
                _cosXml.AbortMultiUpload(abortRequest);
            }
            catch (COSXML.CosException.CosClientException)
            {
                // 忽略错误，会话已经被移除
            }
            catch (COSXML.CosException.CosServerException)
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
        var (bucket, objectKey) = ParsePath(path);

        try
        {
            var localPath = Path.GetTempFileName();
            var getRequest = new GetObjectRequest(bucket, objectKey, localPath, null);
            _cosXml.GetObject(getRequest);

            // 读取临时文件到内存流
            var memoryStream = new MemoryStream();
            using (var fileStream = File.OpenRead(localPath))
            {
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
            }
            // 删除临时文件
            File.Delete(localPath);

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
        var (bucket, objectKey) = ParsePath(path);

        try
        {
            var deleteRequest = new DeleteObjectRequest(bucket, objectKey);
            _cosXml.DeleteObject(deleteRequest);
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
        var (bucket, objectKey) = ParsePath(path);

        try
        {
            var headRequest = new HeadObjectRequest(bucket, objectKey);
            _cosXml.HeadObject(headRequest);
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
        var (bucket, objectKey) = ParsePath(path);

        try
        {
            var headRequest = new HeadObjectRequest(bucket, objectKey);
            var result = _cosXml.HeadObject(headRequest);

            return new FileMetadata
            {
                Name = Path.GetFileName(objectKey),
                Path = path,
                Size = result.size,
                ContentType = result.cosStorageClass,
                LastModified = ParseCosTime(result.lastModified),
                ETag = result.eTag,
                IsDirectory = false,
                Url = GetObjectUrl(bucket, objectKey)
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
        var (bucket, objectKey) = ParsePath(path);

        try
        {
            var request = new PreSignatureStruct
            {
                appid = _options.AppId,
                region = _options.Region,
                bucket = bucket.Replace($"-{_options.AppId}", ""), // 去除AppId后缀
                key = objectKey,
                httpMethod = "GET",
                isHttps = true,
                signDurationSecond = (long)expiresIn.TotalSeconds,
                headers = null,
                queryParameters = null
            };

            var url = _cosXml.GenerateSignURL(request);
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
        var (sourceBucket, sourceKey) = ParsePath(sourcePath);
        var (destBucket, destKey) = ParsePath(destinationPath);

        try
        {
            var copyRequest = new CopyObjectRequest(destBucket, destKey);
            var copySource = new CopySourceStruct(
                _options.AppId,
                sourceBucket,
                _options.Region,
                sourceKey);
            copyRequest.SetCopySource(copySource);
            _cosXml.CopyObject(copyRequest);
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
            var listRequest = new GetBucketRequest(bucket);
            listRequest.SetPrefix(prefix);
            if (!recursive)
            {
                listRequest.SetDelimiter("/");
            }

            GetBucketResult result;
            do
            {
                result = _cosXml.GetBucket(listRequest);

                if (result.listBucket?.contentsList != null)
                {
                    foreach (var content in result.listBucket.contentsList)
                    {
                        files.Add(new FileMetadata
                        {
                            Name = Path.GetFileName(content.key),
                            Path = content.key,
                            Size = content.size,
                            LastModified = DateTimeOffset.TryParse(content.lastModified, out var lm) ? lm : DateTimeOffset.MinValue,
                            ETag = content.eTag,
                            IsDirectory = false,
                            Url = GetObjectUrl(bucket, content.key)
                        });
                    }
                }

                if (result.listBucket != null && !string.IsNullOrEmpty(result.listBucket.nextMarker))
                {
                    listRequest.SetMarker(result.listBucket.nextMarker);
                }
                else
                {
                    break;
                }
            } while (result.listBucket?.isTruncated == true);

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
        var bucket = request.BucketName ?? GetFullBucketName(_options.DefaultBucket);
        var objectKey = NormalizePath(request.StoragePath);

        try
        {
            var putRequest = new PutObjectRequest(bucket, objectKey, request.FileStream);

            if (request.ContentType != null)
            {
                putRequest.SetRequestHeader("Content-Type", request.ContentType);
            }

            if (request.AccessControl != null)
            {
                putRequest.SetCosACL(request.AccessControl);
            }

            if (request.Metadata != null)
            {
                foreach (var kvp in request.Metadata)
                {
                    putRequest.SetRequestHeader($"x-cos-meta-{kvp.Key}", kvp.Value);
                }
            }

            var result = _cosXml.PutObject(putRequest);

            return new FileUploadResult
            {
                Success = true,
                Path = request.StoragePath,
                Url = GetObjectUrl(bucket, objectKey),
                FileSize = request.FileStream.Length,
                ETag = result.eTag
            };
        }
        catch (Exception ex)
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = $"腾讯云COS上传失败: {ex.Message}"
            };
        }
    }

    #region 私有方法

    /// <summary>
    /// 解析 COS 时间格式
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static DateTimeOffset? ParseCosTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var dto))
        {
            return dto;
        }

        return null;
    }

    /// <summary>
    /// 获取完整的存储桶名称
    /// </summary>
    private string GetFullBucketName(string bucketName)
    {
        return $"{bucketName}-{_options.AppId}";
    }

    /// <summary>
    /// 获取对象URL
    /// </summary>
    private string GetObjectUrl(string bucket, string objectKey)
    {
        if (!string.IsNullOrEmpty(_options.CdnDomain))
        {
            return $"https://{_options.CdnDomain}/{objectKey}";
        }

        return $"https://{bucket}.cos.{_options.Region}.myqcloud.com/{objectKey}";
    }

    /// <summary>
    /// 解析路径
    /// </summary>
    private (string bucket, string objectKey) ParsePath(string path)
    {
        var normalizedPath = NormalizePath(path);
        return (GetFullBucketName(_options.DefaultBucket), normalizedPath);
    }

    #endregion

    /// <summary>
    /// 分片上传会话
    /// </summary>
    private class MultipartUploadSession
    {
        public string UploadId { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string ObjectKey { get; set; } = string.Empty;
        public ConcurrentDictionary<int, string> PartETags { get; set; } = new();
    }
}

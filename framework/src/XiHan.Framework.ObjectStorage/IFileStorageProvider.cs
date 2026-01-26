#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFileStorageProvider
// Guid:a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/10 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.ObjectStorage.Models;

namespace XiHan.Framework.ObjectStorage;

/// <summary>
/// 文件存储提供程序接口
/// </summary>
public interface IFileStorageProvider
{
    /// <summary>
    /// 存储类型名称
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 是否支持分片上传
    /// </summary>
    bool SupportChunkedUpload { get; }

    /// <summary>
    /// 是否支持断点续传
    /// </summary>
    bool SupportResumableUpload { get; }

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="request">上传请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传结果</returns>
    Task<FileUploadResult> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 初始化分片上传
    /// </summary>
    /// <param name="request">初始化请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传ID</returns>
    Task<string> InitiateChunkedUploadAsync(ChunkedUploadInitRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 上传文件分片
    /// </summary>
    /// <param name="request">分片上传请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分片上传结果</returns>
    Task<ChunkUploadResult> UploadChunkAsync(ChunkUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成分片上传
    /// </summary>
    /// <param name="request">完成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传结果</returns>
    Task<FileUploadResult> CompleteChunkedUploadAsync(ChunkedUploadCompleteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消分片上传
    /// </summary>
    /// <param name="uploadId">上传ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AbortChunkedUploadAsync(string uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件流</returns>
    Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件元数据</returns>
    Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成预签名URL（用于临时访问）
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="expiresIn">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预签名URL</returns>
    Task<string> GeneratePresignedUrlAsync(string path, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    /// <summary>
    /// 复制文件
    /// </summary>
    /// <param name="sourcePath">源路径</param>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移动文件
    /// </summary>
    /// <param name="sourcePath">源路径</param>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出目录下的文件
    /// </summary>
    /// <param name="path">目录路径</param>
    /// <param name="recursive">是否递归</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件列表</returns>
    Task<List<FileMetadata>> ListFilesAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);
}

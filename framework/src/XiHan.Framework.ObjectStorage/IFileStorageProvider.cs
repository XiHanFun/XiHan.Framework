// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 批量上传文件
    /// </summary>
    /// <remarks>
    /// 按请求顺序逐个上传，结果与请求一一对应。单个文件失败记入对应结果的 <see cref="FileUploadResult.Success"/>
    /// 与 <see cref="FileUploadResult.ErrorMessage"/>，不影响其余文件；取消时抛出 <see cref="OperationCanceledException"/>。
    /// </remarks>
    /// <param name="requests">上传请求列表，不能包含空项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与请求顺序一致的上传结果</returns>
    Task<IReadOnlyList<FileUploadResult>> UploadManyAsync(IReadOnlyList<FileUploadRequest> requests, CancellationToken cancellationToken = default);

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
    /// 删除文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="bucketName">存储桶名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(string path, string? bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除文件
    /// </summary>
    /// <remarks>
    /// 对去重后的每个路径都尝试删除。有路径删除失败时，在全部尝试结束后抛出 <see cref="AggregateException"/>，
    /// 每个内部异常对应一个失败路径；取消时立即抛出 <see cref="OperationCanceledException"/>。
    /// </remarks>
    /// <param name="paths">文件路径列表，不能包含空路径</param>
    /// <param name="bucketName">存储桶名称，为空时使用默认存储桶</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteManyAsync(IReadOnlyList<string> paths, string? bucketName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="bucketName">存储桶名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string path, string? bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件元数据</returns>
    Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="bucketName">存储桶名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件元数据</returns>
    Task<FileMetadata> GetMetadataAsync(string path, string? bucketName, CancellationToken cancellationToken = default);

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

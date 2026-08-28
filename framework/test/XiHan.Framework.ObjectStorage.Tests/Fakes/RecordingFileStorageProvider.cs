// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectStorage.Models;

namespace XiHan.Framework.ObjectStorage.Tests.Fakes;

/// <summary>
/// 记录调用的文件存储提供程序替身
/// </summary>
/// <remarks>
/// 只实现 <see cref="FileStorageProviderBase"/> 要求的抽象成员，不碰任何真实存储介质。
/// 一方面用于验证基类模板方法（UploadAsync 计时与吞异常、带桶名重载转调）的编排逻辑，
/// 另一方面作为管理器/路由器用例里可辨识的 Provider 实例。
/// 必须保留无参构造函数：DI 容器按最贪婪可解析构造函数选择实现，多构造函数会引入歧义。
/// </remarks>
public class RecordingFileStorageProvider : FileStorageProviderBase
{
    /// <summary>
    /// 存储类型名称
    /// </summary>
    public override string ProviderName => "Recording";

    /// <summary>
    /// UploadCoreAsync 被调用的次数
    /// </summary>
    public int UploadCoreCallCount { get; private set; }

    /// <summary>
    /// UploadCoreAsync 要抛出的异常，为空表示正常返回
    /// </summary>
    public Exception? UploadCoreException { get; set; }

    /// <summary>
    /// UploadCoreAsync 的返回值
    /// </summary>
    public FileUploadResult UploadCoreResult { get; set; } = new() { Success = true };

    /// <summary>
    /// DeleteAsync(path, cancellationToken) 收到的路径
    /// </summary>
    public List<string> DeletedPaths { get; } = [];

    /// <summary>
    /// ExistsAsync(path, cancellationToken) 收到的路径
    /// </summary>
    public List<string> ExistsPaths { get; } = [];

    /// <summary>
    /// GetMetadataAsync(path, cancellationToken) 收到的路径
    /// </summary>
    public List<string> MetadataPaths { get; } = [];

    /// <summary>
    /// ExistsAsync 的返回值
    /// </summary>
    public bool ExistsResult { get; set; } = true;

    /// <summary>
    /// 下载文件
    /// </summary>
    public override Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    public override Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        DeletedPaths.Add(path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public override Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        ExistsPaths.Add(path);
        return Task.FromResult(ExistsResult);
    }

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    public override Task<FileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        MetadataPaths.Add(path);
        return Task.FromResult(new FileMetadata { Name = path, Path = path });
    }

    /// <summary>
    /// 复制文件
    /// </summary>
    public override Task CopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 移动文件
    /// </summary>
    public override Task MoveAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 列出目录下的文件
    /// </summary>
    public override Task<List<FileMetadata>> ListFilesAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<FileMetadata>());
    }

    /// <summary>
    /// 暴露基类的路径规范化方法，供测试断言
    /// </summary>
    public string CallNormalizePath(string path)
    {
        return NormalizePath(path);
    }

    /// <summary>
    /// 暴露基类的扩展名提取方法，供测试断言
    /// </summary>
    public string CallGetFileExtension(string fileName)
    {
        return GetFileExtension(fileName);
    }

    /// <summary>
    /// 暴露基类的哈希计算方法，供测试断言
    /// </summary>
    public Task<string> CallComputeFileHashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return ComputeFileHashAsync(stream, cancellationToken);
    }

    /// <summary>
    /// 上传文件的核心实现
    /// </summary>
    protected override Task<FileUploadResult> UploadCoreAsync(FileUploadRequest request, CancellationToken cancellationToken)
    {
        UploadCoreCallCount++;

        if (UploadCoreException is not null)
        {
            throw UploadCoreException;
        }

        return Task.FromResult(UploadCoreResult);
    }
}

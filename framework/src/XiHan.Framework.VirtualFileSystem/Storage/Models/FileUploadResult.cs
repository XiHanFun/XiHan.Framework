#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileUploadResult
// Guid:c3d4e5f6-a7b8-49c0-d1e2-f3a4b5c6d7e8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/10 10:06:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Storage.Models;

/// <summary>
/// 文件上传结果
/// </summary>
public class FileUploadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// 完整路径（绝对路径）
    /// </summary>
    public string? FullPath { get; set; }

    /// <summary>
    /// 访问URL
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 文件哈希值（ETag）
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// 上传耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 额外信息
    /// </summary>
    public Dictionary<string, object>? Extra { get; set; }
}

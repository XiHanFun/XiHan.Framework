#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SftpFile
// Guid:ee5f9d7c-bfae-4564-ac15-96192a469d5e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/27 15:35:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.IO;

namespace XiHan.Framework.Utils.Net.Sftp;

/// <summary>
/// SFTP文件信息
/// </summary>
public class SftpFile
{
    /// <summary>
    /// 文件完整路径
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// 文件名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastWriteTime { get; set; }

    /// <summary>
    /// 最后访问时间
    /// </summary>
    public DateTime LastAccessTime { get; set; }

    /// <summary>
    /// 文件权限（Unix格式，如0755）
    /// </summary>
    public int Permissions { get; set; }

    /// <summary>
    /// 文件权限字符
    /// </summary>
    public string? PermissionString { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 组ID
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// 扩展名（不包含点）
    /// </summary>
    public string Extension
    {
        get
        {
            if (IsDirectory)
            {
                return string.Empty;
            }

            var lastDotIndex = Name.LastIndexOf('.');
            return lastDotIndex > 0 ? Name[(lastDotIndex + 1)..] : string.Empty;
        }
    }

    /// <summary>
    /// 获取文件大小的可读形式
    /// </summary>
    /// <returns>格式化后的文件大小（如：1.5 MB）</returns>
    public string GetReadableFileSize()
    {
        return IsDirectory ? "-" : Length.FormatFileSizeToString();
    }
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PhysicalFileProviderOptions
// Guid:b4f2c6b3-626c-4265-9b91-edd7c4b673bc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:38:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Physical;

/// <summary>
/// 物理文件提供器选项
/// </summary>
public class PhysicalFileProviderOptions
{
    /// <summary>
    /// 是否启用文件监视
    /// </summary>
    public bool EnableFileWatch { get; set; }

    /// <summary>
    /// 文件监视过滤器
    /// </summary>
    public string FileWatchFilter { get; set; } = "*.*";

    /// <summary>
    /// 是否包含子目录
    /// </summary>
    public bool IncludeSubdirectories { get; set; } = true;

    /// <summary>
    /// 是否跟踪符号链接
    /// </summary>
    public bool FollowSymlinks { get; set; }

    /// <summary>
    /// 缓冲区大小（字节）
    /// </summary>
    public int BufferSize { get; set; } = 4096;
}

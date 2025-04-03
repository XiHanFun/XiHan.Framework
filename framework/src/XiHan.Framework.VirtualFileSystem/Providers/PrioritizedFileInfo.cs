#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PrioritizedFileInfo
// Guid:425fb1fc-6c10-4bf9-8479-5712b7b53b43
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 6:20:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Providers;

/// <summary>
/// 优先级文件信息包装器
/// </summary>
public class PrioritizedFileInfo : IFileInfo
{
    /// <summary>
    /// 内部文件信息
    /// </summary>
    private readonly IFileInfo _inner;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner"></param>
    /// <param name="priority"></param>
    public PrioritizedFileInfo(IFileInfo inner, int priority)
    {
        _inner = inner;
        Priority = priority;
    }

    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists => _inner.Exists;

    /// <summary>
    /// 文件长度
    /// </summary>
    public long Length => _inner.Length;

    /// <summary>
    /// 物理路径
    /// </summary>
    public string? PhysicalPath => _inner.PhysicalPath;

    /// <summary>
    /// 名称
    /// </summary>
    public string Name => _inner.Name;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset LastModified => _inner.LastModified;

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory => _inner.IsDirectory;

    /// <summary>
    /// 创建读取流
    /// </summary>
    /// <returns></returns>
    public Stream CreateReadStream()
    {
        return _inner.CreateReadStream();
    }
}

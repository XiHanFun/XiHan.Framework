#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MemoryFile
// Guid:7a0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 7:30:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Providers.Memory;

/// <summary>
/// 内存文件实体
/// </summary>
public class MemoryFile
{
    /// <summary>
    /// 初始化内存文件
    /// </summary>
    /// <param name="path"></param>
    /// <param name="content"></param>
    public MemoryFile(string path, byte[] content)
    {
        Path = path;
        Content = content;
        LastModified = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 更新内容
    /// </summary>
    /// <param name="newContent"></param>
    /// <returns></returns>
    public MemoryFile UpdateContent(byte[] newContent)
    {
        Content = newContent;
        LastModified = DateTimeOffset.UtcNow;
        return this;
    }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 文件内容
    /// </summary>
    public byte[] Content { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset LastModified { get; set; }
}

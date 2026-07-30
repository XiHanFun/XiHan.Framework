// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.VirtualFileSystem.Events;

/// <summary>
/// 文件变化事件参数
/// </summary>
public class FileChangedEventArgs : EventArgs
{
    /// <summary>
    /// 初始化文件变化事件参数
    /// </summary>
    /// <param name="filePath">变化的文件路径</param>
    /// <param name="changeType">文件变化类型</param>
    public FileChangedEventArgs(string filePath, FileChangeType changeType)
    {
        FilePath = filePath;
        ChangeType = changeType;
    }

    /// <summary>
    /// 变化的文件路径
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// 文件变化类型
    /// </summary>
    public FileChangeType ChangeType { get; }
}

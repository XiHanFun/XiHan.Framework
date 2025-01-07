#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileChangeEventArgs
// Guid:5ddbbecb-ef26-4f25-a931-ad78a89860d2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:36:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 文件变更事件参数
/// </summary>
public class FileChangeEventArgs : EventArgs
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// 变更类型
    /// </summary>
    public WatcherChangeTypes ChangeType { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="changeType"></param>
    public FileChangeEventArgs(string filePath, WatcherChangeTypes changeType)
    {
        FilePath = filePath;
        ChangeType = changeType;
    }
}

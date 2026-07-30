// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.VirtualFileSystem.Events;

/// <summary>
/// 文件变化类型
/// </summary>
public enum FileChangeType
{
    /// <summary>
    /// 新增
    /// </summary>
    Created,

    /// <summary>
    /// 修改
    /// </summary>
    Modified,

    /// <summary>
    /// 删除
    /// </summary>
    Deleted
}

#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WatcherChangeTypes
// Guid:3ad4f8b1-675f-4008-abab-faaa768a301e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/8 5:50:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 文件监视变更类型
/// </summary>
public enum WatcherChangeTypes
{
    /// <summary>
    /// 创建
    /// </summary>
    Created,

    /// <summary>
    /// 变更
    /// </summary>
    Changed,

    /// <summary>
    /// 删除
    /// </summary>
    Deleted,

    /// <summary>
    /// 修改
    /// </summary>
    Modified,

    /// <summary>
    /// 重命名
    /// </summary>
    Renamed
}

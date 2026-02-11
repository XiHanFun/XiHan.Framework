#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileChangeType
// Guid:41f21fff-d5b6-4f7a-84ba-16a06867b68b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/24 04:15:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

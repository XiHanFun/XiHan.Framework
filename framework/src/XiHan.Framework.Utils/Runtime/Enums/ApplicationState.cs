#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationState
// Guid:93d6711b-49ea-4104-870f-5a50c70e3f88
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:43:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Runtime.Enums;

/// <summary>
/// 应用程序状态枚举
/// </summary>
public enum ApplicationState
{
    /// <summary>
    /// 未初始化
    /// </summary>
    Uninitialized = 0,

    /// <summary>
    /// 正在初始化
    /// </summary>
    Initializing = 1,

    /// <summary>
    /// 正在运行
    /// </summary>
    Running = 2,

    /// <summary>
    /// 正在暂停
    /// </summary>
    Pausing = 3,

    /// <summary>
    /// 已暂停
    /// </summary>
    Paused = 4,

    /// <summary>
    /// 正在恢复
    /// </summary>
    Resuming = 5,

    /// <summary>
    /// 正在关闭
    /// </summary>
    Shutting = 6,

    /// <summary>
    /// 已关闭
    /// </summary>
    Shutdown = 7,

    /// <summary>
    /// 发生错误
    /// </summary>
    Error = 8
}

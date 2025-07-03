#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationExitReason
// Guid:c5c66d91-17a9-4618-b681-bf69f41cab19
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:42:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Runtime.Enums;

/// <summary>
/// 应用程序退出原因枚举
/// </summary>
public enum ApplicationExitReason
{
    /// <summary>
    /// 正常退出
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 用户请求退出
    /// </summary>
    UserRequested = 1,

    /// <summary>
    /// 系统关闭
    /// </summary>
    SystemShutdown = 2,

    /// <summary>
    /// 异常退出
    /// </summary>
    Exception = 3,

    /// <summary>
    /// 强制退出
    /// </summary>
    Forced = 4,

    /// <summary>
    /// 内存不足
    /// </summary>
    OutOfMemory = 5,

    /// <summary>
    /// 重启
    /// </summary>
    Restart = 6
}
